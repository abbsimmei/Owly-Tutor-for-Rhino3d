// The placement shared by the popovers that point at something in the transcript (plans 020 and 023).
// The panel carries a CSS zoom, so the position is worked out in the panel's own unzoomed units: the scale
// is measured, which cancels the zoom whether the engine reports rects zoomed or not.

/** Puts `root` just below `anchor` (or above it when there is no room), inside the stream. False when it can't measure. */
export function placeBeside(root: HTMLElement, anchor: HTMLElement): boolean {
  const panel = root.closest<HTMLElement>('.panel');
  const stream = panel?.querySelector<HTMLElement>('.stream');
  const view = panel?.querySelector<HTMLElement>('.transcript');
  if (!panel || !stream || !view) return false;
  const pr = panel.getBoundingClientRect();
  const scale = pr.width / panel.offsetWidth || 1;
  const ar = anchor.getBoundingClientRect();
  const sr = stream.getBoundingClientRect();
  const vr = view.getBoundingClientRect();
  const viewTop = (vr.top - pr.top) / scale;
  const bottom = (vr.bottom - pr.top) / scale;
  // At a high zoom 300px is wider than the stream: narrow it first, so the measurements below are the final size.
  root.style.maxWidth = `${Math.min(300, sr.width / scale)}px`;
  const width = root.offsetWidth;
  const height = root.offsetHeight;

  const minLeft = (sr.left - pr.left) / scale;
  const maxLeft = Math.max(minLeft, (sr.right - pr.left) / scale - width);
  const left = Math.min(Math.max((ar.left - pr.left) / scale, minLeft), maxLeft);
  let top = (ar.bottom - pr.top) / scale + 4;
  // Flip above only when it doesn't fit below and there is more room above; never rise over the header.
  if (top + height > bottom && (ar.top - pr.top) / scale - viewTop > bottom - top)
    top = (ar.top - pr.top) / scale - height - 4;
  top = Math.max(top, viewTop);

  root.style.left = `${Math.round(left)}px`;
  root.style.top = `${Math.round(top)}px`;
  return true;
}
