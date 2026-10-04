# Community gallery

`gallery.json` is what CrosshairY shows in **Discover › Community gallery**. The app downloads it from this repo (`raw.githubusercontent.com`) and keeps a copy for offline use. `previews/` holds a preview image of each published design.

## How submissions work (automated)

1. Someone clicks **Share yours** in the app. It opens a [Gallery submission](../.github/ISSUE_TEMPLATE/gallery.yml) issue with the name and code filled in.
2. The [Gallery workflow](../.github/workflows/gallery.yml) checks it within a minute or two:
   - **Valid:** it comments a preview image and a summary (layers, animated, recoil) and labels the issue `gallery`.
   - **Problem:** it comments what's wrong and labels it `needs-fix`. When the author edits the issue, it's checked again.
3. **You approve:** look at the preview and add the **`approved`** label. Only people with write access to the repo can approve.
4. The workflow adds the design to `gallery.json` (committed as Jonah), saves its preview to `previews/`, comments "Published" and closes the issue. Everyone sees it in the app within about an hour, or right away if they press refresh.

Not approving? Just close the issue. Duplicates of an existing design are detected and closed automatically.

Any code CrosshairY can import is accepted (CrosshairY, Crosshair X, VALORANT, CS2). It's stored as a CrosshairY code so the gallery works offline. Image layers must be embedded, and the review comment flags them so you look at the preview before approving.

## By hand

The tool also runs locally: `.\tools\gallery.ps1 -Mode review -Issue <number> -DryRun` prints the comment without posting anything.

To add an entry manually, append to `crosshairs`:

```json
{ "id": "short-unique-slug", "name": "Design name", "author": "credit", "category": "Classic", "added": "YYYY-MM-DD", "code": "CXY1-…" }
```

An `id` must never change once published: the app uses it to know which gallery designs someone has already saved.
