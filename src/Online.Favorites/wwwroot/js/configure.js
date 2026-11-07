// Configure page only: save a row when focus leaves it after a change.
// Without this script the page still works: pressing Enter in a field saves the row.
for (const form of document.querySelectorAll("form[data-autosave]")) {
    let changed = false;
    form.addEventListener("input", () => { changed = true; });
    form.addEventListener("change", () => { changed = true; });

    form.addEventListener("focusout", (event) => {
        // Moving between fields of the same row (e.g. name → URL) waits until you leave the row.
        if (!changed || form.contains(event.relatedTarget)) return;
        if (!form.reportValidity()) return;
        changed = false;
        form.requestSubmit(form.querySelector(".default-submit"));
    });
}
