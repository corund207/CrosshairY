# Community gallery

`gallery.json` is what CrosshairY shows in **Discover › Community gallery**. The app downloads it from this repo (`raw.githubusercontent.com`) and keeps a copy for offline use.

## Adding a design

1. People submit designs with **Share yours** in the app. It opens a [Gallery submission](../.github/ISSUE_TEMPLATE/gallery.yml) issue with the name and CrosshairY code filled in.
2. Check the design: import the code in CrosshairY (Ctrl+I).
3. Append an entry to `crosshairs` and close the issue:

```json
{
  "id": "short-unique-slug",
  "name": "Design name",
  "author": "credit name",
  "category": "Classic",
  "added": "YYYY-MM-DD",
  "code": "CXY1-…"
}
```

`id` must never change once published: the app uses it to know which gallery designs someone has already saved.
