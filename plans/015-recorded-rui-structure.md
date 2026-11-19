## Recorded `.rui` structure

Recorded by the plan 015 executor from `%APPDATA%\McNeel\Rhinoceros\9.0\UI\default.rui` (read-only, about 4.7 MB). Element names, counts and single-line script strings only.

- **2.1** R9 file read. Rhino 8: `%APPDATA%\McNeel\Rhinoceros\8.0` does not exist on this machine ("R8 not available on this machine"; the R8 check happens in the manual Rhino check).
- **2.2** Structure confirmed. Root children: `extend_rhino_menus`, `menus`, `tool_bar_groups`, `tool_bars`, `macros`, `icons`, `bitmaps`, `scripts`.
  - `tool_bar_group`: 7. `tool_bar_group_item`: 22, each with one `tool_bar_id` (22).
  - `sidebar`: 19 (children of `tool_bar_group_item`).
  - `tool_bar`: 125. `tool_bar_item`: 1845. `left_macro_id`: 1781. `right_macro_id`: 506. `link`: 191.
  - `macro_item`: 2277 (all in `macros`). `script`: 2269. `tooltip`: 253. `button_text`: 1773. `text`: 1940 (all elements together).
  - `locale_*` children exist (for example `locale_1033`, `locale_1031`, `locale_1036`).
  - `platforms` attribute: `macro_item="Win"` 50, `tool_bar="Win"` 4, `menu_item="Win"` 3, `menu="Win"` 1, `tool_bar_item="Mac"` 1.
  - `tool_bar_group` attributes: `active_tool_bar_group`, `dock_bar_guid64`, `guid`, `hide_single_tab`, `point_floating`, `single_file`.
  - Unexpected: `tool_bar` itself also carries `platforms="Win"` in 4 places. Plan 015's rules filter only `tool_bar_item` and `macro_item`.
- **2.3** Buttons whose script's first command is `Loft`: exactly 2.
  1. `! _Loft` (left macro). On the toolbar "Surface Creation", reached via `link` from the toolbar in group "Main" (flyout, 1 level).
  2. `! _Loft` (left macro). On the toolbar "Surface Sidebar", which is the `tool_bar_id` of a group item in group "Surface Sidebar" (a tab toolbar, no link).
  No `right_macro_id` Loft buttons, no `platforms` attribute on either item or macro. Both are covered by the step 3 rules (tab `tool_bar_id`, flyout `link`).
- **2.4** not run: needs Rhino.
- **2.6** STOP conditions: none hit (files are readable XML, macros are in `macros/macro_item`, Loft is reached via covered rules).
