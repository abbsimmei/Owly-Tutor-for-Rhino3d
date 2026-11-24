// Black-box checks against the built panel in a real browser: markdown, highlighting, layout at
// docked widths, streaming, and the one architectural claim worth asserting - that an earlier turn
// is never rebuilt when a later one streams.
//
// Uses the locally installed Chrome via puppeteer-core. Skips (exit 0) when no browser is found, so
// it is safe in an environment without one. Pass --shots to also write dist/shots/*.png.

import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const page_url = `file://${resolve(root, 'dist/panel.html')}`;
const shots = process.argv.includes('--shots');
const shotDir = resolve(root, 'dist/shots');

const CANDIDATES = [
  '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
  '/Applications/Chromium.app/Contents/MacOS/Chromium',
  '/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge',
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  '/usr/bin/google-chrome',
  '/usr/bin/chromium',
];

const executablePath = process.env.CHROME_PATH ?? CANDIDATES.find((p) => existsSync(p));
if (!executablePath) {
  console.log('verify: no Chrome/Edge found, skipping browser checks');
  process.exit(0);
}
if (!existsSync(resolve(root, 'dist/panel.html'))) {
  console.error('verify: dist/panel.html is missing, run `npm run build` first');
  process.exit(1);
}

let puppeteer;
try {
  puppeteer = (await import('puppeteer-core')).default;
} catch {
  console.log('verify: puppeteer-core is not installed, skipping browser checks');
  process.exit(0);
}

const failures = [];
const check = (name, ok, detail = '') => {
  if (ok) console.log(`  ok    ${name}`);
  else {
    console.log(`  FAIL  ${name}${detail ? ` - ${detail}` : ''}`);
    failures.push(name);
  }
};

const wait = (ms) => new Promise((r) => setTimeout(r, ms));

/** Polls a page expression or function; returns the first truthy result, or null on timeout. */
async function until(page, fnOrExpression, timeoutMs = 4000, everyMs = 60) {
  const deadline = Date.now() + timeoutMs;
  for (;;) {
    const value = await page.evaluate(fnOrExpression);
    if (value) return value;
    if (Date.now() > deadline) return null;
    await wait(everyMs);
  }
}

const browser = await puppeteer.launch({
  executablePath,
  headless: true,
  timeout: 30000,
  args: ['--no-sandbox', '--disable-gpu', '--force-color-profile=srgb'],
});

try {
  const page = await browser.newPage();
  page.setDefaultTimeout(8000);

  // Plan 010: the owl must never sit on a backdrop of our own making, so transparent art just works.
  const owlsClear = (scope) => page.evaluate((sel) => {
    const owls = [...document.querySelectorAll(sel)];
    const bad = owls.filter((o) => {
      const s = getComputedStyle(o);
      return !(s.backgroundColor === 'rgba(0, 0, 0, 0)' || s.backgroundColor === 'transparent')
        || s.backgroundImage !== 'none' || s.filter !== 'none' || s.mixBlendMode !== 'normal' || s.borderRadius !== '0px';
    });
    return { count: owls.length, bad: bad.length };
  }, scope);
  await page.setViewport({ width: 1180, height: 880, deviceScaleFactor: shots ? 2 : 1 });

  const consoleProblems = [];
  page.on('console', (m) => { if (m.type() === 'error') consoleProblems.push(m.text()); });
  page.on('pageerror', (e) => consoleProblems.push(String(e.message)));
  page.on('error', (e) => consoleProblems.push(`crash: ${e.message}`));

  if (shots) mkdirSync(shotDir, { recursive: true });
  const shot = async (name) => { if (shots) await page.screenshot({ path: resolve(shotDir, `${name}.png`) }); };

  await page.goto(page_url, { waitUntil: 'domcontentloaded' });
  await wait(800);
  await shot('01-seeded');

  const type = async (text) => {
    await page.click('.composer textarea');
    await page.type('.composer textarea', text, { delay: 2 });
  };

  // ---------------------------------------------------------------- markdown
  const md = await page.evaluate(() => {
    const q = (s) => document.querySelectorAll(s).length;
    return {
      strong: q('.msg-agent strong'),
      inlineCode: q('.msg-agent p code'),
      bullets: q('.msg-agent ul li'),
      quote: q('.msg-agent blockquote'),
      tableRows: q('.msg-agent .md-table tbody tr'),
      tableHeads: q('.msg-agent .md-table th'),
      rawPipes: [...document.querySelectorAll('.msg-agent p')].filter((p) => p.textContent.includes('| ---')).length,
      strayMarkers: [...document.querySelectorAll('.msg-agent')].filter((m) => /\*\*|`{1,3}/.test(m.textContent)).length,
      spacedLink: [...document.querySelectorAll('.msg-agent a')].filter((a) =>
        a.getAttribute('href')?.endsWith('/Rhino renders/planarity heatmap.png'),
      ).length,
      rawDestinations: [...document.querySelectorAll('.msg-agent')].filter((m) => m.textContent.includes('](<')).length,
    };
  });
  check('markdown renders bold', md.strong > 0);
  check('markdown renders inline code', md.inlineCode > 0);
  check('markdown renders bullet lists', md.bullets >= 4);
  check('markdown renders blockquotes', md.quote > 0);
  check('markdown renders tables', md.tableRows >= 3 && md.tableHeads === 3, JSON.stringify(md));
  check('table syntax is not left as a paragraph', md.rawPipes === 0);
  check('no unconsumed markdown markers leak into text', md.strayMarkers === 0);
  check('a bracketed destination carries a path with a space in it', md.spacedLink === 1, JSON.stringify(md));
  check('a bracketed destination is not left as literal text', md.rawDestinations === 0);

  // ------------------------------------------------------------ image block
  const picture = await page.evaluate(() => {
    const block = document.querySelector('.img-block');
    const img = block?.querySelector('img');
    return {
      blocks: document.querySelectorAll('.img-block').length,
      name: block?.querySelector('figcaption .name')?.textContent ?? '',
      size: block?.querySelector('figcaption .size')?.textContent ?? '',
      save: block?.querySelectorAll('.img-save').length ?? 0,
      wide: img ? Math.round(img.getBoundingClientRect().width) : 0,
      column: block ? Math.round(block.getBoundingClientRect().width) : 0,
      popOut: block?.querySelectorAll('.img-frame .img-open').length ?? 0,
    };
  });
  check('an image the agent produced draws as its own block', picture.blocks >= 1);
  check('the image block names the file, its pixels and its size',
    picture.name.endsWith('.png') && /× \d+ · [\d.]+ (MB|KB)/.test(picture.size), JSON.stringify(picture));
  check('the image block offers a save button', picture.save === 1);
  check('the image fills the transcript column', picture.wide > 0 && picture.wide === picture.column,
    JSON.stringify(picture));
  check('the image carries a pop-out button over its corner', picture.popOut === 1, JSON.stringify(picture));

  // ------------------------------------------------------- no tool lines
  const card = await page.evaluate(() => ({ tools: document.querySelectorAll('.tool').length }));
  check('no tool-call line renders', card.tools === 0, JSON.stringify(card));
  const owlsSeeded = await owlsClear('.header .owl, .agent-head .owl');
  check('the owl shows in the header and on each answer, with nothing painted behind it',
    owlsSeeded.count >= 2 && owlsSeeded.bad === 0, JSON.stringify(owlsSeeded));

  // ------------------------------------------------ streaming + reconciliation
  await page.evaluate(() => {
    // Tag every existing turn so we can tell a survivor from a rebuild.
    document.querySelectorAll('.turn').forEach((t, i) => { t.dataset.probe = `probe-${i}`; });
  });
  const before = await page.evaluate(() => document.querySelectorAll('.turn').length);

  await type('How do I loft a parametric facade');
  await page.keyboard.press('Enter');

  // Catch the caret while the first assistant run is still arriving, and tag that exact element so
  // the next sample proves the delta grew it rather than replacing it.
  const streaming = await until(page, () => {
    const block = document.querySelector('.msg-agent.streaming');
    if (!block) return null;
    block.dataset.probe = 'streaming-block';
    return { length: block.textContent.length };
  });
  check('the live block shows a streaming caret', streaming !== null);

  const baseline = streaming?.length ?? 0;
  const grown = await until(
    page,
    `(() => {
       const block = document.querySelector('.msg-agent[data-probe="streaming-block"]');
       if (!block) return null;
       const length = block.textContent.length;
       return length > ${baseline} ? { length } : null;
     })()`,
  );
  check('a delta grows the same element in place', grown !== null, `stuck at ${baseline} chars`);

  const mid = await page.evaluate(() => ({
    turns: document.querySelectorAll('.turn').length,
    probesKept: document.querySelectorAll('.turn[data-probe]').length,
    thinking: document.querySelectorAll('.status-strip').length,
    stopButton: document.querySelectorAll('.send.stop').length,
    tools: document.querySelectorAll('.tool').length,
    turnText: document.querySelector('.turn:last-of-type')?.textContent.length ?? 0,
  }));
  check('a new turn is appended', mid.turns === before + 1, `${before} -> ${mid.turns}`);
  check('earlier turns are not rebuilt', mid.probesKept === before, `${mid.probesKept}/${before} survived`);
  check('a status strip reports progress', mid.thinking === 1);
  const owlStrip = await owlsClear('.status-strip .owl');
  check('the thinking strip shows the owl', owlStrip.count === 1 && owlStrip.bad === 0, JSON.stringify(owlStrip));
  check('send becomes stop while running', mid.stopButton === 1);
  await shot('03-streaming');

  const later = await until(
    page,
    `(() => {
       const turn = document.querySelector('.turn:last-of-type');
       const length = turn ? turn.textContent.length : 0;
       return length > ${mid.turnText} ? { length } : null;
     })()`,
    8000,
  );
  check('the running turn grows as it proceeds', later !== null, `stuck at ${mid.turnText} chars`);
  check('no tool line appears while the turn runs',
    (await page.evaluate(() => document.querySelectorAll('.tool').length)) === 0 && mid.tools === 0);
  check('earlier turns still are not rebuilt mid-turn',
    (await page.evaluate(() => document.querySelectorAll('.turn[data-probe]').length)) === before);

  // Autoscroll runs off a ResizeObserver, which fires after layout, so assert that it settles
  // pinned rather than that it is pinned at one arbitrary instant.
  const pinned = await until(
    page,
    `(() => {
       const t = document.querySelector('.transcript');
       const gap = t.scrollHeight - t.scrollTop - t.clientHeight;
       return gap < 30 ? { gap } : null;
     })()`,
    2000,
  );
  check('the transcript settles pinned to the tail', pinned !== null, 'never reached the bottom');

  // The counterpart: a real gesture must still unpin, or the user cannot read back mid-turn.
  const centre = await page.evaluate(() => {
    const r = document.querySelector('.transcript').getBoundingClientRect();
    return { x: Math.round(r.left + r.width / 2), y: Math.round(r.top + r.height / 2) };
  });
  await page.mouse.move(centre.x, centre.y);
  await page.mouse.wheel({ deltaY: -400 });
  const unpinned = await until(page, `document.querySelectorAll('.jump button').length === 1 ? { shown: true } : null`, 3000);
  check('scrolling up unpins and offers a jump back', unpinned !== null);

  if (unpinned) {
    await page.click('.jump button');
    const repinned = await until(
      page,
      `(() => {
         const t = document.querySelector('.transcript');
         return document.querySelectorAll('.jump').length === 0 &&
           t.scrollHeight - t.scrollTop - t.clientHeight < 30 ? { back: true } : null;
       })()`,
      3000,
    );
    check('the jump pill returns to the tail and re-pins', repinned !== null);
  }

  // ------------------------------------------------------------------- escape
  await page.click('.composer textarea');
  await page.keyboard.press('Escape');
  await wait(300);
  const cancelled = await page.evaluate(() => ({
    stop: document.querySelectorAll('.send.stop').length,
    cancelledNote: [...document.querySelectorAll('.lifecycle')].some((n) => n.textContent === 'stopped'),
  }));
  check('escape cancels the running turn', cancelled.stop === 0 && cancelled.cancelledNote);

  // ------------------------------------------------------- composer affordances
  await type('/');
  await wait(250);
  const slash = await page.evaluate(() => document.querySelectorAll('.mention-menu .menu-item').length);
  check('slash opens the command menu', slash >= 3, `${slash} commands`);
  await page.keyboard.press('Escape');
  await wait(150);

  // ------------------------------------------------------------------- layout
  for (const [label, preset] of [['narrow', 'Narrow'], ['docked', 'Docked'], ['wide', 'Wide'], ['full', 'Full']]) {
    await page.evaluate((p) => {
      [...document.querySelectorAll('.devbar button')].find((b) => b.textContent === p)?.click();
    }, preset);
    await wait(350);
    const overflow = await page.evaluate(() => {
      const t = document.querySelector('.transcript');
      const c = document.querySelector('.composer');
      return {
        transcript: t.scrollWidth - t.clientWidth,
        composer: c.scrollWidth - c.clientWidth,
        width: Math.round(document.querySelector('.panel').getBoundingClientRect().width),
      };
    });
    check(`no horizontal overflow at ${label} (${overflow.width}px)`, overflow.transcript <= 1 && overflow.composer <= 1, JSON.stringify(overflow));
  }
  await page.evaluate(() => {
    [...document.querySelectorAll('.devbar button')].find((b) => b.textContent === 'Docked')?.click();
  });

  // -------------------------------------------------------------- question card
  await page.evaluate(() => {
    [...document.querySelectorAll('.header .icon-btn')][0].click();
  });
  await wait(300);
  const empty = await page.evaluate(() => ({
    starters: document.querySelectorAll('.starter').length,
    turns: document.querySelectorAll('.turn').length,
  }));
  check('a new conversation shows the empty state', empty.turns === 0 && empty.starters === 3, JSON.stringify(empty));
  const owlEmpty = await owlsClear('.empty .owl');
  check('the empty state shows the owl', owlEmpty.count === 1 && owlEmpty.bad === 0, JSON.stringify(owlEmpty));

  await type('audit the selected objects');
  await page.keyboard.press('Enter');
  await wait(9000);
  const question = await page.evaluate(() => {
    const composer = document.querySelector('.composer').getBoundingClientRect();
    const overComposer = document.elementFromPoint(composer.x + composer.width / 2, composer.y + composer.height / 2);
    const transcript = document.querySelector('.transcript').getBoundingClientRect();
    const overTranscript = document.elementFromPoint(transcript.x + transcript.width / 2, transcript.y + 8);
    return {
      scrims: document.querySelectorAll('.ask-scrim').length,
      cards: document.querySelectorAll('.ask').length,
      pages: document.querySelectorAll('.ask-page').length,
      steps: document.querySelectorAll('.ask .steps i').length,
      count: document.querySelector('.ask-count')?.textContent,
      options: document.querySelectorAll('.ask .opts input').length,
      dontKnow: document.querySelector('.ask .synth label span')?.textContent,
      backHidden: document.querySelector('.ask-row .btn:not(.ghost):not(.primary)')?.hidden,
      nextLabel: document.querySelector('.ask .btn.primary')?.textContent,
      nextDisabled: document.querySelector('.ask .btn.primary')?.disabled,
      cancelLabel: document.querySelector('.ask .btn.ghost')?.textContent,
      composerReachable: !document.querySelector('.ask-scrim').contains(overComposer),
      transcriptReachable: !document.querySelector('.ask-scrim').contains(overTranscript),
      inHeader: document.querySelector('.header').contains(document.querySelector('.ask-scrim')),
      focusInCard: document.querySelector('.ask').contains(document.activeElement),
      activeTag: document.activeElement?.tagName,
      activeClass: document.activeElement?.className,
      thinking: document.querySelectorAll('.status-strip').length,
    };
  });
  const owlAsk = await owlsClear('.ask-head .owl');
  check('the question card shows the owl', owlAsk.count === 1 && owlAsk.bad === 0, JSON.stringify(owlAsk));
  // A batch is paged, not stacked: one card showing one question at a time.
  check('a batch of questions opens one paged card',
    question.scrims === 1 && question.cards === 1 && question.pages === 1
    && question.steps === 2 && question.count === 'Question 1 of 2'
    && question.options === 3, JSON.stringify(question));
  check('the overlay blocks the composer and the transcript',
    question.composerReachable === false && question.transcriptReachable === false && question.inHeader === false,
    JSON.stringify(question));
  check('the first page offers no Back', question.backHidden === true);
  check('focus moves into the card, so the composer caret is not left blinking behind the scrim',
    question.focusInCard === true, `${question.activeTag}.${question.activeClass}`);
  check('the way out is Cancel, not Skip', question.cancelLabel === 'Cancel', question.cancelLabel);

  // A 1px band of a different cursor is a flicker you cannot rest in, so every band has to be
  // wide enough to land on: gaps between clickable rows must belong to a row, not the container.
  const bands = await page.evaluate(() => {
    const card = document.querySelector('.ask').getBoundingClientRect();
    const x = card.x + card.width / 2;
    const runs = [];
    for (let y = Math.ceil(card.y); y < card.y + card.height; y++) {
      const node = document.elementFromPoint(x, y);
      const cursor = node ? getComputedStyle(node).cursor : 'none';
      const last = runs[runs.length - 1];
      if (last && last.cursor === cursor) last.px++;
      else runs.push({ cursor, px: 1 });
    }
    return runs;
  });
  check('the pointer never crosses a sliver band over the card',
    bands.every((band) => band.px >= 8), JSON.stringify(bands));
  check('Next is offered before the last page', question.nextLabel === 'Next', question.nextLabel);
  check('Next stays disabled until the page is answered', question.nextDisabled === true);
  check('every page offers "I don\'t know"', question.dontKnow === "I don't know", question.dontKnow);
  check('no thinking cue while blocked on the question', question.thinking === 0);
  await shot('05-question');

  // "I don't know" is an ANSWER: it arms Next rather than cancelling the question.
  await page.evaluate(() => document.querySelector('.ask .synth label').click());
  await wait(150);
  const deferred = await page.evaluate(() => ({
    nextDisabled: document.querySelector('.ask .btn.primary').disabled,
    optionsChecked: [...document.querySelectorAll('.ask .opts input')].filter((i) => i.checked).length,
  }));
  check('"I don\'t know" answers the page and clears any pick',
    deferred.nextDisabled === false && deferred.optionsChecked === 0, JSON.stringify(deferred));

  // Picking a real option retracts it, and the two must never both be set.
  await page.evaluate(() => document.querySelector('.ask .opts label').click());
  await wait(150);
  const retracted = await page.evaluate(() => ({
    dontKnowChecked: document.querySelector('.ask .synth input').checked,
    optionsChecked: [...document.querySelectorAll('.ask .opts input')].filter((i) => i.checked).length,
  }));
  check('picking an option retracts "I don\'t know"',
    retracted.dontKnowChecked === false && retracted.optionsChecked === 1, JSON.stringify(retracted));

  await page.evaluate(() => document.querySelector('.ask .btn.primary').click());
  await wait(400);
  const second = await page.evaluate(() => ({
    pages: document.querySelectorAll('.ask-page').length,
    count: document.querySelector('.ask-count')?.textContent,
    backHidden: document.querySelector('.ask-row .btn:not(.ghost):not(.primary)')?.hidden,
    nextLabel: document.querySelector('.ask .btn.primary')?.textContent,
    stepsDone: document.querySelectorAll('.ask .steps i.done').length,
  }));
  check('Next turns the page rather than opening a second card',
    second.pages === 1 && second.count === 'Question 2 of 2', JSON.stringify(second));
  check('the last page offers Back and Answer all',
    second.backHidden === false && second.nextLabel === 'Answer all', JSON.stringify(second));
  check('the stepper marks the answered page', second.stepsDone === 1, JSON.stringify(second));

  // Keyboard reaches the card only while focus is inside it, so Escape is the cheapest proof.
  await page.keyboard.press('Escape');
  await wait(250);
  check('Escape cancels from the keyboard', (await page.evaluate(() => document.querySelectorAll('.ask').length)) === 0);

  await page.evaluate(() => [...document.querySelectorAll('.header .icon-btn')][0].click());
  await wait(300);
  await type('audit the selected objects');
  await page.keyboard.press('Enter');
  await wait(9000);
  await page.evaluate(() => document.querySelector('.ask .opts label').click());
  await wait(150);
  await page.keyboard.press('Enter');
  await wait(400);
  check('Enter turns the page from the keyboard',
    (await page.evaluate(() => document.querySelector('.ask-count')?.textContent)) === 'Question 2 of 2');

  await page.evaluate(() => document.querySelector('.ask .opts label').click());
  await wait(150);
  await page.evaluate(() => document.querySelector('.ask .btn.primary').click());
  await wait(3500);
  const answered = await page.evaluate(() => ({
    scrims: document.querySelectorAll('.ask-scrim').length,
    continued: /Closed it via/.test(document.querySelector('.turn:last-of-type')?.textContent ?? ''),
  }));
  check('answering closes the overlay and the turn continues', answered.scrims === 0 && answered.continued, JSON.stringify(answered));
  await shot('06-answered');

  // -------------------------------------------------- too-old engine fallback
  const old = await browser.newPage();
  old.setDefaultTimeout(6000);
  await old.setViewport({ width: 900, height: 700 });
  await old.evaluateOnNewDocument(() => {
    const real = CSS.supports.bind(CSS);
    CSS.supports = (...args) =>
      String(args[0]).includes('container-type') ? false : real(...args);
  });
  await old.goto(page_url, { waitUntil: 'domcontentloaded' });
  await wait(500);
  const gate = await old.evaluate(() => ({
    notice: document.querySelectorAll('.unsupported').length,
    panel: document.querySelectorAll('.panel').length,
    override: document.querySelectorAll('.unsupported .btn').length,
  }));
  check('an unsupported engine gets a notice, not a broken panel',
    gate.notice === 1 && gate.panel === 0 && gate.override === 1, JSON.stringify(gate));

  await old.click('.unsupported .btn');
  await wait(700);
  const overridden = await old.evaluate(() => ({
    notice: document.querySelectorAll('.unsupported').length,
    panel: document.querySelectorAll('.panel').length,
    turns: document.querySelectorAll('.turn').length,
  }));
  check('"show it anyway" starts the panel', overridden.notice === 0 && overridden.panel === 1 && overridden.turns > 0,
    JSON.stringify(overridden));
  await old.close();

  check('no css color-mix left in the shipped stylesheet',
    !(await page.evaluate(() => [...document.styleSheets]
      .flatMap((sheet) => { try { return [...sheet.cssRules]; } catch { return []; } })
      .some((rule) => rule.cssText.includes('color-mix')))));

  // --------------------------------------------------------- notices / status
  await page.evaluate(() => [...document.querySelectorAll('.header .icon-btn')][1].click());
  await wait(300);
  const placement = await page.evaluate(() => {
    const notice = document.querySelector('.notice');
    const header = document.querySelector('.header');
    const transcript = document.querySelector('.transcript');
    const composer = document.querySelector('.composer');
    if (!notice || !header || !transcript) return null;
    return {
      belowHeader: notice.getBoundingClientRect().top >= header.getBoundingClientRect().bottom,
      nearTop: notice.getBoundingClientRect().top < transcript.getBoundingClientRect().top + 60,
      hasComposer: composer !== null,
    };
  });
  check('a notice sits just under the header', placement !== null && placement.belowHeader && placement.nearTop,
    JSON.stringify(placement));

  const cleared = await until(page, `document.querySelectorAll('.notice').length === 0 ? { gone: true } : null`, 7000);
  check('a notice clears itself without being dismissed', cleared !== null);

  const settings = () => page.evaluate(() => [...document.querySelectorAll('.header .icon-btn')][1].click());
  await settings();
  await wait(1200);
  await settings();
  await wait(1200);
  await settings();
  const repeated = await page.evaluate(() => document.querySelectorAll('.notice').length);
  check('the same notice fired three times stays one toast', repeated === 1, `count ${repeated}`);
  // Past the point the first firing alone would have expired, so this only holds if a repeat restarts the clock.
  await wait(2000);
  const alive = await page.evaluate(() => document.querySelectorAll('.notice').length);
  check('a repeat restarts the countdown', alive === 1, `count ${alive}`);
  await until(page, `document.querySelectorAll('.notice').length === 0 ? { gone: true } : null`, 7000);

  await type('How do I loft a parametric facade');
  await page.keyboard.press('Enter');
  const strip = await until(
    page,
    `(() => {
       const s = document.querySelector('.status-strip');
       const t = document.querySelector('.transcript');
       return s ? { belowTranscript: s.getBoundingClientRect().top >= t.getBoundingClientRect().bottom - 1 } : null;
     })()`,
  );
  check('the status strip sits below the transcript', strip?.belowTranscript === true, JSON.stringify(strip));
  await page.click('.composer textarea');
  await page.keyboard.press('Escape');
  await wait(300);

  // -------------------------------------------------------- zoom / right click
  const zoomOf = () => page.evaluate(() => document.querySelector('.panel').style.zoom || '1');

  // Without a host there is nothing to draw a native menu, so the browser must keep its own.
  const browserMenuKept = await page.evaluate(() => {
    let prevented = null;
    const probe = (e) => { prevented = e.defaultPrevented; };
    window.addEventListener('contextmenu', probe);
    document.querySelector('.transcript').dispatchEvent(
      new MouseEvent('contextmenu', { bubbles: true, cancelable: true }),
    );
    window.removeEventListener('contextmenu', probe);
    return { prevented, inPageMenus: document.querySelectorAll('.ctx-menu').length };
  });
  check('with no host, right click leaves the browser menu alone',
    browserMenuKept.prevented === false && browserMenuKept.inPageMenus === 0, JSON.stringify(browserMenuKept));

  await page.keyboard.down(process.platform === 'darwin' ? 'Meta' : 'Control');
  await page.keyboard.press('Equal');
  await page.keyboard.up(process.platform === 'darwin' ? 'Meta' : 'Control');
  await wait(150);
  check('the keyboard shortcut zooms in', (await zoomOf()) === '1.1', await zoomOf());

  await page.keyboard.down(process.platform === 'darwin' ? 'Meta' : 'Control');
  await page.keyboard.press('Digit0');
  await page.keyboard.up(process.platform === 'darwin' ? 'Meta' : 'Control');
  await wait(150);
  check('the reset shortcut returns to 100%', (await zoomOf()) === '1', await zoomOf());

  const panelCentre = await page.evaluate(() => {
    const r = document.querySelector('.panel').getBoundingClientRect();
    return { x: Math.round(r.left + r.width / 2), y: Math.round(r.top + r.height / 3) };
  });
  await page.mouse.move(panelCentre.x, panelCentre.y);
  await page.keyboard.down('Control');
  await page.mouse.wheel({ deltaY: -120 });
  await page.keyboard.up('Control');
  await wait(200);
  check('ctrl and the wheel zooms', (await zoomOf()) === '1.1', await zoomOf());
  await page.evaluate(() => { document.querySelector('.panel').style.zoom = '1'; });

  await page.click('.composer textarea');
  const box = await page.evaluate(() => {
    const r = document.querySelector('.composer textarea').getBoundingClientRect();
    return { x: Math.round(r.left + r.width / 2), y: Math.round(r.top + r.height / 2) };
  });
  await page.mouse.click(box.x, box.y, { button: 'right' });
  await wait(200);
  check('a text field keeps its native menu so paste still works',
    (await page.evaluate(() => document.querySelectorAll('.ctx-menu').length)) === 0);

  // ---------------------------------------------------------------- projects (plan 025)
  const count = (sel) => page.evaluate((s) => document.querySelectorAll(s).length, sel);
  const projectHeader = () => page.evaluate(() => document.querySelector('.header .project-btn .project-name')?.textContent ?? '');
  check('the header shows the project name', (await projectHeader()) === 'Tower study', await projectHeader());
  check('the project button sits between the agent chip and the new conversation button',
    (await page.evaluate(() => {
      const kids = [...document.querySelector('.header').children];
      const chip = kids.findIndex((k) => k.classList.contains('agent-chip'));
      const project = kids.findIndex((k) => k.classList.contains('project-btn'));
      return chip >= 0 && project === chip + 1 && kids[project + 1]?.classList.contains('icon-btn');
    })) === true);
  check('a long project name truncates in a capped button',
    (await page.evaluate(() => getComputedStyle(document.querySelector('.project-btn')).maxWidth === '140px'
      && getComputedStyle(document.querySelector('.project-btn .project-name')).textOverflow === 'ellipsis')) === true);
  check('a file with a project gets no project card', (await count('.project-card')) === 0);

  await page.click('.project-btn');
  await wait(150);
  const projectMenu = await page.evaluate(() => ({
    open: document.querySelectorAll('.project-menu').length,
    items: [...document.querySelectorAll('.project-menu .project-item')].map((n) => n.textContent),
    selected: document.querySelector('.project-menu .project-item.selected')?.textContent ?? '',
    actions: ['new', 'rename', 'delete', 'move'].filter((a) => document.querySelector(`.project-menu .project-${a}`)).length,
  }));
  check('the project menu opens with every project and its actions',
    projectMenu.open === 1 && projectMenu.items.length === 3 && projectMenu.selected === 'Tower study' && projectMenu.actions === 4,
    JSON.stringify(projectMenu));

  await page.click('.project-menu .project-delete');
  await wait(100);
  const confirmText = await page.evaluate(() => document.querySelector('.project-menu .project-confirm')?.textContent ?? '');
  check('delete asks for confirmation inline', confirmText.includes('Tower study'), confirmText);
  await page.click('.project-menu .btn.danger');
  await wait(200);
  check('the menu closes once the project is deleted', (await count('.project-menu')) === 0);
  check('the header says the file has no project yet', (await projectHeader()) === 'No project yet', await projectHeader());

  const projectCard = await page.evaluate(() => ({
    cards: document.querySelectorAll('.project-card').length,
    choices: [...document.querySelectorAll('.project-card .project-choice')].map((n) => n.className),
    first: document.querySelector('.project-card .project-choice')?.textContent ?? '',
    aboveComposer: (() => {
      const card = document.querySelector('.project-card');
      const composer = document.querySelector('.composer');
      return Boolean(card && composer && card.compareDocumentPosition(composer) & Node.DOCUMENT_POSITION_FOLLOWING);
    })(),
  }));
  check('a pending file with a question asked shows the project card above the composer',
    projectCard.cards === 1 && projectCard.aboveComposer, JSON.stringify(projectCard));
  check('the card puts the suggestion first and offers New project and Unsorted',
    projectCard.first.includes('Summer pavilion') && projectCard.choices[0]?.includes('suggested')
      && projectCard.choices.some((c) => c.includes('project-new')) && projectCard.choices.at(-1)?.includes('unsorted'),
    JSON.stringify(projectCard));

  await page.click('.project-card .project-choice.unsorted');
  await wait(200);
  check('choosing a project hides the card', (await count('.project-card')) === 0);
  check('the header shows the chosen project', (await projectHeader()) === 'Unsorted', await projectHeader());

  // ------------------------------------------------- the C# contract, replayed
  // host-events.json is written by PanelContractTests in the plug-in's test suite, using the real
  // serialiser. Replaying it here is the only place the two languages are checked against each
  // other, so a protocol change on either side fails before it reaches Rhino.
  const contractPath = resolve(root, 'tests/host-events.json');
  const hostPage = resolve(root, 'dist/panel.host.html');
  if (!existsSync(contractPath) || !existsSync(hostPage)) {
    check('the C# contract sample is available', false, 'run `dotnet test tests/StreamJson.Tests` and `npm run build`');
  } else {
    const events = JSON.parse(readFileSync(contractPath, 'utf8'));
    const host = await browser.newPage();
    host.setDefaultTimeout(6000);
    host.on('pageerror', (e) => consoleProblems.push(`[host pageerror] ${e.message}`));
    // Nothing serves /image/ here, so the contract sample's image is expected to 404 and fall back.
    host.on('console', (m) => {
      if (m.type() !== 'error') return;
      if (m.location()?.url?.includes('/image/')) return;
      consoleProblems.push(`[host] ${m.text().slice(0, 200)}`);
    });
    await host.setViewport({ width: 420, height: 900 });

    // Stand in for Eto's injected shim, which is what the panel talks to inside Rhino.
    await host.evaluateOnNewDocument(() => {
      // Both pages are file:// and share a localStorage, so the mock page's zoom would leak in.
      try { window.localStorage.clear(); } catch { /* opaque origin */ }
      window.__sent = [];
      window.eto = { postMessage: (message) => window.__sent.push(JSON.parse(message)) };
    });
    await host.goto(`file://${hostPage}`, { waitUntil: 'domcontentloaded' });
    await wait(400);

    const handshake = await host.evaluate(() => window.__sent.map((m) => m.type));
    check('the host build talks to Eto and announces itself', handshake.includes('ready'), JSON.stringify(handshake));
    check('the host build ships no mock host',
      (await host.evaluate(() => document.querySelectorAll('.devbar').length)) === 0);

    // Staged: the first batch leaves the turn mid-flight, so a running tool still spins and offers its chip.
    const settling = (event) =>
      event.type === 'turn.end' || (event.type === 'turn.tool.patch' && event.patch.status === 'unknown');
    await host.evaluate((batch) => { for (const event of batch) window.rhinoAI.receive(event); },
      events.filter((event) => !settling(event)));
    await wait(600);

    const rendered = await host.evaluate(() => ({
      agent: document.querySelector('.agent-chip .name')?.textContent,
      model: document.querySelector('.agent-chip .model')?.textContent,
      turns: document.querySelectorAll('.turn').length,
      prompt: document.querySelector('.msg-user')?.textContent,
      bold: document.querySelectorAll('.msg-agent strong').length,
      code: document.querySelectorAll('.md-code').length,
      tools: document.querySelectorAll('.tool').length,
      question: document.querySelectorAll('.ask .opts label').length,
      questionPages: document.querySelectorAll('.ask-page').length,
      questionSteps: document.querySelectorAll('.ask .steps i').length,
      footerTokens: [...document.querySelectorAll('.turn-foot')].map((n) => n.textContent).join(' '),
      images: document.querySelectorAll('.img-block').length,
      imageSrc: document.querySelector('.img-block img')?.getAttribute('src'),
      imageFrames: document.querySelectorAll('.img-block .img-frame:not([hidden])').length,
      imageName: document.querySelector('.img-block figcaption .name')?.textContent ?? '',
    }));

    check('C#-serialised events render a turn', rendered.turns === 1 && rendered.prompt?.includes('Facade'), JSON.stringify(rendered));
    check('the agent list arrives', rendered.agent === 'Claude Code' && rendered.model === 'Opus 5', JSON.stringify(rendered));
    check('streamed markdown and code render', rendered.bold >= 1 && rendered.code === 1, JSON.stringify(rendered));
    check('tool calls from the host draw no line', rendered.tools === 0, JSON.stringify(rendered));
    check('a C#-serialised image lands as a block pointing at the host route',
      rendered.images === 1 && rendered.imageSrc === '/image/a3f1c09e42b7', JSON.stringify(rendered));
    check('an image the host cannot serve drops the picture and keeps its caption',
      rendered.imageFrames === 0 && rendered.imageName.length > 0, JSON.stringify(rendered));
    check('a uuid filename is labelled rather than shown',
      rendered.imageName === 'Generated image', JSON.stringify(rendered));
    check('the mcp__rhino__ prefix never reaches the panel',
      !JSON.stringify(rendered).includes('mcp__'), JSON.stringify(rendered));
    check('the questions posed by the feed open one paged card',
      rendered.questionPages === 1 && rendered.questionSteps === 2 && rendered.question === 2, JSON.stringify(rendered));
    check('per-turn tokens land on the turn, not the top bar',
      rendered.footerTokens.includes('14k tok') && !rendered.footerTokens.includes('$'),
      JSON.stringify(rendered.footerTokens));
    check('the top bar carries no token or cost readout',
      (await host.evaluate(() => document.querySelectorAll('.header .usage').length)) === 0);
    check('no cost is shown anywhere',
      (await host.evaluate(() => !document.body.textContent.includes('$0.'))) === true);

    // Plan 014: a Rhino command in an answer is a copy chip; other inline code stays plain.
    await host.evaluate(() => window.rhinoAI.receive({ type: 'turn.text', turnId: 'turn-0', blockId: 'turn-0-cmd',
      delta: 'Use `_Loft` here. The layer is `layer_name`.' }));
    await wait(200);
    check('a Rhino command in an answer renders as a copy chip',
      (await host.evaluate(() => [...document.querySelectorAll('.msg-agent .md-cmd-copy code')]
        .some((el) => el.textContent === '_Loft'))) === true);
    check('inline code that is not a command stays plain code',
      (await host.evaluate(() => [...document.querySelectorAll('code')]
        .some((el) => el.textContent === 'layer_name' && !el.closest('.md-cmd')))) === true);
    // Plan 019: only the first mention of a command in an answer is a chip.
    await host.evaluate(() => window.rhinoAI.receive({ type: 'turn.text', turnId: 'turn-0', blockId: 'turn-0-cmd',
      delta: ' Later `_Loft` again.' }));
    await wait(200);
    check('a repeated command in one answer makes one chip and one plain code',
      (await host.evaluate(() => {
        const block = [...document.querySelectorAll('.msg-agent')].find((el) => el.textContent.includes('Later'));
        return block.querySelectorAll('.md-cmd').length === 1
          && [...block.querySelectorAll('code')].filter((c) => c.textContent === '_Loft' && !c.closest('.md-cmd')).length === 1;
      })) === true);
    await host.evaluate(() => { document.querySelector('.md-cmd-copy').click(); });
    await wait(100);
    check('clicking a chip sends command.copy with the name',
      (await host.evaluate(() => window.__sent.some((m) => m.type === 'command.copy' && m.name === '_Loft'))) === true);
    check('clicking a chip shows the paste hint',
      (await host.evaluate(() => [...document.querySelectorAll('.notice.info')]
        .some((el) => el.textContent.includes('_Loft')))) === true);

    // Plan 020 (the old card's class name is split so a grep for the removed code finds nothing): the toolbar.found line of the contract fixture was never asked for, so the replay opens nothing.
    check('the contract replay opens no location popover and draws no location card',
      (await host.evaluate(() => document.querySelectorAll('.location-popover, .location-' + 'card').length)) === 0);
    const findCount = () => host.evaluate(() => window.__sent.filter((m) => m.type === 'toolbar.find').length);
    const finds = await findCount();
    await host.evaluate(() => { document.querySelector('.md-cmd-where').click(); });
    await wait(150);
    check('clicking the where button sends toolbar.find with the name and turn',
      (await host.evaluate(() => window.__sent.some((m) => m.type === 'toolbar.find' && m.name === '_Loft' && m.turnId === 'turn-0'))) === true);
    const looking = await host.evaluate(() => {
      const pop = document.querySelector('.location-popover');
      return {
        count: document.querySelectorAll('.location-popover').length,
        role: pop?.getAttribute('role') ?? '',
        text: pop?.textContent ?? '',
        focusOnClose: pop !== null && pop.querySelector('button') === document.activeElement,
      };
    });
    check('the where click opens one location popover with role dialog, still looking',
      looking.count === 1 && looking.role === 'dialog' && looking.text.includes('Looking for'), JSON.stringify(looking));
    check('focus moves to the popover close button', looking.focusOnClose === true);
    await host.evaluate((event) => window.rhinoAI.receive(event),
      events.find((event) => event.type === 'toolbar.found'));
    await wait(100);
    check('the host answer puts Surface Tools in the open popover',
      (await host.evaluate(() => document.querySelector('.location-popover')?.textContent.includes('Surface Tools') ?? false)) === true);
    const placed = await host.evaluate(() => {
      const pop = document.querySelector('.location-popover').getBoundingClientRect();
      const chip = document.querySelector('.md-cmd[data-cmd="_Loft"]').getBoundingClientRect();
      const panel = document.querySelector('.panel').getBoundingClientRect();
      return { pop: [pop.left, pop.top, pop.right, pop.bottom], chip: [chip.left, chip.top, chip.right, chip.bottom], panel: [panel.left, panel.right] };
    });
    check('the popover sits next to its chip inside the panel',
      placed.pop[0] >= placed.panel[0] - 1 && placed.pop[2] <= placed.panel[1] + 1
      && (Math.abs(placed.pop[1] - placed.chip[3]) < 12 || Math.abs(placed.pop[3] - placed.chip[1]) < 12), JSON.stringify(placed));
    await host.keyboard.press('Escape');
    await wait(100);
    check('Escape closes the popover and its scrim',
      (await host.evaluate(() => document.querySelectorAll('.location-popover, .scrim').length)) === 0);
    await host.evaluate(() => { document.querySelector('.md-cmd-where').click(); });
    await wait(100);
    check('a second where click opens at once from the cache, without a new toolbar.find',
      (await host.evaluate(() => document.querySelector('.location-popover')?.textContent.includes('Surface Tools') ?? false)) === true
      && (await findCount()) === finds + 1);
    await host.evaluate(() => { document.querySelector('.scrim').click(); });
    await wait(100);
    check('a click outside closes the popover',
      (await host.evaluate(() => document.querySelectorAll('.location-popover, .scrim').length)) === 0);
    check('no location card exists any more',
      (await host.evaluate(() => document.querySelectorAll('.location-' + 'card').length)) === 0);

    // Plan 025: the contract's project event is pending, so the card asks, suggestion first.
    const hostProject = await host.evaluate(() => ({
      cards: document.querySelectorAll('.project-card').length,
      first: document.querySelector('.project-card .project-choice')?.textContent ?? '',
      header: document.querySelector('.project-btn .project-name')?.textContent ?? '',
    }));
    check('a C#-serialised pending project shows the card with the suggestion first',
      hostProject.cards === 1 && hostProject.first.includes('Pavilion') && hostProject.header === 'No project yet',
      JSON.stringify(hostProject));
    await host.evaluate(() => { document.querySelector('.project-card .project-choice.suggested').click(); });
    await wait(100);
    check('choosing on the card sends project.choose with the id',
      (await host.evaluate(() => window.__sent.some((m) => m.type === 'project.choose' && m.id === 'p-1'))) === true);
    await host.evaluate((event) => window.rhinoAI.receive({ ...event, id: 'p-1', name: 'Pavilion', pending: false, suggestion: undefined }),
      events.find((event) => event.type === 'project'));
    await wait(100);
    const hostChosen = await host.evaluate(() => ({
      cards: document.querySelectorAll('.project-card').length,
      header: document.querySelector('.project-btn .project-name')?.textContent ?? '',
    }));
    check('the host naming the project hides the card and shows the name',
      hostChosen.cards === 0 && hostChosen.header === 'Pavilion', JSON.stringify(hostChosen));

    await host.evaluate((batch) => { for (const event of batch) window.rhinoAI.receive(event); },
      events.filter(settling));
    const ended = await host.evaluate(() => ({
      tools: document.querySelectorAll('.tool').length,
      spinners: document.querySelectorAll('.transcript .spinner').length,
    }));
    check('an ended turn with unfinished calls draws no tool line and no spinner',
      ended.tools === 0 && ended.spinners === 0, JSON.stringify(ended));

    // Switching Rhino's theme sends a second theme event. The panel has to restyle from it, both
    // the host-supplied tokens and the scheme attribute that drives everything the host does not
    // send (semantic and syntax colours). The attribute is set by an effect, so each event needs a
    // turn of the microtask queue before it can be read back.
    const readTheme = () =>
      host.evaluate(() => ({
        scheme: document.documentElement.dataset.scheme,
        // The transcript is transparent and shows the body's ground.
        bg: getComputedStyle(document.body).backgroundColor,
        chrome: getComputedStyle(document.querySelector('.header')).backgroundColor,
      }));

    await host.evaluate(() =>
      window.rhinoAI.receive({
        type: 'theme',
        scheme: 'light',
        tokens: { bg: '#ffffff', control: '#ebebed', text: '#111111' },
      }),
    );
    await wait(120);
    const lightTheme = await readTheme();

    await host.evaluate(() =>
      window.rhinoAI.receive({
        type: 'theme',
        scheme: 'dark',
        tokens: { bg: '#141417', control: '#1c1c21', text: '#eeeeee' },
      }),
    );
    await wait(120);
    const darkTheme = await readTheme();

    check('a second theme event restyles the panel',
      lightTheme.bg === 'rgb(255, 255, 255)' && darkTheme.bg === 'rgb(20, 20, 23)',
      JSON.stringify({ lightTheme, darkTheme }));
    check('the chrome and the content restyle independently',
      darkTheme.chrome === 'rgb(28, 28, 33)' && darkTheme.chrome !== darkTheme.bg,
      JSON.stringify(darkTheme));
    check('the scheme attribute follows, so unsent tokens switch too',
      lightTheme.scheme === 'light' && darkTheme.scheme === 'dark',
      JSON.stringify({ light: lightTheme.scheme, dark: darkTheme.scheme }));

    // Without any host tokens the stylesheet has to carry a complete theme on its own, or a page
    // that has not been sent one (a fresh load, a suppressed send) renders half in each theme.
    const stylesheetThemes = await host.evaluate(() => {
      const names = ['--bg', '--control', '--control-hover', '--field', '--text', '--link',
                     '--selection', '--selection-text', '--rule', '--surface', '--border', '--shadow'];
      const root = document.documentElement;
      const saved = names.map((n) => [n, root.style.getPropertyValue(n)]);
      for (const [n] of saved) root.style.removeProperty(n);

      const read = () => Object.fromEntries(
        names.map((n) => [n, getComputedStyle(root).getPropertyValue(n).trim()]));

      root.dataset.scheme = 'light';
      const light = read();
      root.dataset.scheme = 'dark';
      const dark = read();

      for (const [n, v] of saved) if (v) root.style.setProperty(n, v);
      return { light, dark, missing: names.filter((n) => !light[n] || !dark[n]),
               same: names.filter((n) => light[n] === dark[n]) };
    });
    check('the stylesheet defines every theme token in both schemes',
      stylesheetThemes.missing.length === 0, JSON.stringify(stylesheetThemes.missing));
    check('every theme token actually changes between the schemes',
      stylesheetThemes.same.length === 0, JSON.stringify(stylesheetThemes.same));

    // A host that re-announces what it already sent must not double the transcript. This is the
    // receiver half of the guard; the sender half is ConversationFeed keeping its high-water marks.
    const beforeReplay = await host.evaluate(() => ({
      turns: document.querySelectorAll('.turn').length,
      users: document.querySelectorAll('.msg-user').length,
    }));
    await host.evaluate((batch) => { for (const event of batch) window.rhinoAI.receive(event); }, events.filter((e) => e.type.startsWith('turn.')));
    await wait(400);
    const afterReplay = await host.evaluate(() => ({
      turns: document.querySelectorAll('.turn').length,
      users: document.querySelectorAll('.msg-user').length,
    }));
    check('re-announced turns do not duplicate rows',
      JSON.stringify(beforeReplay) === JSON.stringify(afterReplay),
      `${JSON.stringify(beforeReplay)} -> ${JSON.stringify(afterReplay)}`);

    // Right click is the host's job now, so the panel must ask rather than draw.
    await host.evaluate(() => {
      window.__sent.length = 0;
      document.querySelector('.transcript').dispatchEvent(
        new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 120, clientY: 260 }),
      );
    });
    await wait(150);
    const menuRequest = await host.evaluate(() => window.__sent.find((m) => m.type === 'menu.open'));
    check('right click asks the host for a native menu',
      menuRequest !== undefined && menuRequest.x === 120 && menuRequest.y === 260, JSON.stringify(menuRequest));
    check('the request carries what the menu items need',
      menuRequest?.zoomLabel === '100%' && menuRequest.canZoomIn === true && menuRequest.canResetZoom === false,
      JSON.stringify(menuRequest));
    check('no in-page menu is drawn',
      (await host.evaluate(() => document.querySelectorAll('.ctx-menu').length)) === 0);

    // A text field keeps the native field menu, which is the only route to Paste.
    await host.evaluate(() => {
      window.__sent.length = 0;
      document.querySelector('.composer textarea').dispatchEvent(
        new MouseEvent('contextmenu', { bubbles: true, cancelable: true }),
      );
    });
    await wait(120);
    check('a text field is left to the native field menu',
      (await host.evaluate(() => window.__sent.some((m) => m.type === 'menu.open'))) === false);

    await host.evaluate(() => {
      window.__sent.length = 0;
      document.querySelector('.header').dispatchEvent(
        new MouseEvent('contextmenu', { bubbles: true, cancelable: true }),
      );
    });
    await wait(120);
    check('the header gets no zoom menu',
      (await host.evaluate(() => window.__sent.some((m) => m.type === 'menu.open'))) === false);

    // The menu reports an intent; the panel owns the ladder.
    await host.evaluate(() => window.rhinoAI.receive({ type: 'zoom', action: 'in' }));
    await wait(150);
    check('a zoom event from the host moves the panel one rung',
      (await host.evaluate(() => document.querySelector('.panel').style.zoom)) === '1.1',
      await host.evaluate(() => document.querySelector('.panel').style.zoom));

    // Answering has to travel back in the shape the C# deserialiser accepts: one submit carrying
    // every question posed, even though they were answered a page at a time.
    await host.evaluate(() => document.querySelector('.ask .opts label').click());
    await wait(150);
    await host.evaluate(() => document.querySelector('.ask .btn.primary').click());
    await wait(300);
    await host.evaluate(() => document.querySelector('.ask .synth label').click());
    await wait(150);
    await host.evaluate(() => document.querySelector('.ask .btn.primary').click());
    await wait(200);
    const answered = await host.evaluate(() => window.__sent.find((m) => m.type === 'question.answer'));
    check('one submit answers every question in the shape C# parses',
      answered?.items?.length === 2
      && answered.items[0].answers[0] === 'Yes'
      && answered.items[1].answers[0] === "I don't know"
      && answered.items.every((i) => typeof i.id === 'string'), JSON.stringify(answered));

    // ------------- the paper clip: a WKWebView has no open panel, so the host picks and answers with attachments.add
    await host.evaluate(() => {
      window.__sent.length = 0;
      document.querySelector('.composer-actions button[title="Attach a file"]').click();
    });
    await wait(120);
    check('the paper clip asks the host to open a file dialog',
      await host.evaluate(() => window.__sent.some((m) => m.type === 'attachments.pick')),
      await host.evaluate(() => JSON.stringify(window.__sent.map((m) => m.type))));

    const picked = {
      id: 'host-1',
      kind: 'image',
      name: 'plan.png',
      mediaType: 'image/png',
      bytes: 68,
      dataUrl: 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==',
    };
    await host.evaluate((attachment) => {
      window.__sent.length = 0;
      window.rhinoAI.receive({ type: 'attachments.add', attachments: [attachment] });
    }, picked);
    await wait(200);
    const tile = await host.evaluate(() => ({
      tiles: document.querySelectorAll('.composer .attach').length,
      thumbnails: document.querySelectorAll('.composer .attach img').length,
      name: document.querySelector('.composer .attach .meta b')?.textContent,
    }));
    check('a file the host picked lands in the composer with its thumbnail',
      tile.tiles === 1 && tile.thumbnails === 1 && tile.name === 'plan.png', JSON.stringify(tile));

    await host.evaluate(() => {
      const field = document.querySelector('.composer textarea');
      field.value = 'what is this';
      field.dispatchEvent(new Event('input', { bubbles: true }));
      field.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));
    });
    await wait(200);
    const sentPrompt = await host.evaluate(() => window.__sent.find((m) => m.type === 'prompt'));
    check('the prompt carries the attachment bytes the host has to decode',
      sentPrompt?.request?.attachments?.length === 1
      && sentPrompt.request.attachments[0].dataUrl === picked.dataUrl
      && sentPrompt.request.attachments[0].kind === 'image', JSON.stringify(sentPrompt?.request?.attachments));
    check('a sent prompt clears the composer of its attachments',
      (await host.evaluate(() => document.querySelectorAll('.composer .attach').length)) === 0);

    // ------------------------------------------------- replay, the way Rhino replays it
    // ConversationFeed.Replay posts an empty snapshot and then the turns one event at a time, each
    // its own ExecuteScriptAsync, and every agent message renders its markdown a frame later. So
    // the transcript is still growing long after the snapshot lands: a replay that scrolls once
    // stops halfway up. Fed here the same way, with the panel left scrolled up beforehand.
    const line = 'The facade layer holds 148 surfaces, most of them planar panels on the south elevation. ';
    const replay = (session, count) => {
      const out = [{ type: 'conversation', snapshot: { sessionId: session, agent: 'claude', docTitle: 'tower-study.3dm', startedAt: '2026-01-01T09:00:00.0000000+00:00', turns: [] } }];
      for (let i = 0; i < count; i++) {
        const id = `${session}-turn-${i}`;
        out.push({ type: 'turn.begin', turn: { id, prompt: `Question ${i}`, attachments: [], context: [], startedAt: '2026-01-01T09:00:00.0000000+00:00', status: 'running', blocks: [], plan: [], undoable: true } });
        out.push({ type: 'turn.text', turnId: id, blockId: `${id}-b1`, delta: line.repeat(3) });
        out.push({ type: 'turn.end', turnId: id, status: 'ok' });
      }
      return out;
    };
    const post = async (batch) => {
      for (const event of batch) {
        await host.evaluate((e) => window.rhinoAI.receive(e), event);
        await wait(20);
      }
    };
    const tail = () => host.evaluate(() => {
      const t = document.querySelector('.transcript');
      return { gap: Math.round(t.scrollHeight - t.scrollTop - t.clientHeight), turns: document.querySelectorAll('.turn').length };
    });

    // Plan 020: the open popover belongs to the conversation, so the empty snapshot that opens a replay closes it.
    const firstReplay = replay('sess-a', 8);
    await host.evaluate(() => { document.querySelector('.md-cmd-where').click(); });
    await wait(100);
    check('a location popover is showing before the replay starts',
      (await host.evaluate(() => document.querySelectorAll('.location-popover').length)) === 1);
    await host.evaluate((e) => window.rhinoAI.receive(e), firstReplay[0]);
    await wait(100);
    check('the empty conversation snapshot of a replay closes the popover and its scrim',
      (await host.evaluate(() => document.querySelectorAll('.location-popover, .scrim').length)) === 0);
    await post(firstReplay.slice(1));
    await wait(1600);
    await host.evaluate(() => document.querySelector('.transcript').scrollTo({ top: 0 }));
    await wait(200);
    await post(replay('sess-b', 8));
    await wait(1600);
    const landed = await tail();
    check('a staged replay lands on the newest message, not partway up',
      landed.turns === 8 && landed.gap < 30, JSON.stringify(landed));

    // The counterpart: a replay must not fight the user reading back through it.
    const reading = replay('sess-c', 8);
    await post(reading.slice(0, 12));
    const seat = await host.evaluate(() => {
      const r = document.querySelector('.transcript').getBoundingClientRect();
      return { x: Math.round(r.left + r.width / 2), y: Math.round(r.top + r.height / 2) };
    });
    await host.mouse.move(seat.x, seat.y);
    await host.mouse.wheel({ deltaY: -4000 });
    await post(reading.slice(12));
    await wait(1600);
    const readingBack = await tail();
    check('a gesture during a replay keeps the user where they scrolled to',
      readingBack.gap > 200, JSON.stringify(readingBack));

    await host.close();
  }

  check('no console errors or crashes', consoleProblems.length === 0, consoleProblems.join(' | '));
} finally {
  await browser.close();
}

console.log(`\n${failures.length === 0 ? 'verify: all checks passed' : `verify: ${failures.length} failed`}`);
process.exit(failures.length === 0 ? 0 : 1);
