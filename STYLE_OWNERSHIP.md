# Interface ownership

React owns the storefront footer, hero content, navigation utilities, page mode
classes, catalog media and admin navigation. Do not restore DOM scripts that
replace these components or overwrite catalog settings.

Styles use the ordered layers `theme, base, components, utilities, site, layout`.
Component rules belong to `site`; explicit responsive/state corrections belong
to `layout`. Forced declaration priority is prohibited. The PostCSS normalizer
preserves media/selector contexts and the build contract checks both source and
generated assets. Server HTML styles are placed in the same cascade, and adjacent
local stylesheet groups are served through `/ui.css` without reordering modules.

`r123-security.js` owns the fetch transport and CSRF handling. Loading and
feedback subscribe to its request lifecycle events. Feedback ignores results
from an earlier navigation and reports explicit operations rather than page
initialization. FormData retains its browser-generated content type.

`dom-lifecycle.js` supplies `InokskarDOM.Observer` subscriptions over one shared
site observer. Subscribers retain their targets, mutation filters and disconnect
behavior. Vendor observers remain vendor-owned.

`inokskar-logo.png` is the original supplied artwork. `inokskar-logo.svg` embeds
the same artwork in a cropped viewport; it does not regenerate the logo. Old
asset URLs redirect to the canonical asset. A one-time catalog revision updates
the existing header logo while permitting subsequent admin changes.

`wwwroot` and the warranty print vendor bundle are generated, not source files.
Build through `npm run build`; publishing fails if the current interface output
is missing. The Docker build generates the interface before .NET publishing.
