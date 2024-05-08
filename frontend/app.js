function formatDate(s) {
    return new Date(s).toLocaleString("en-GB", { dateStyle: "short" });
}

function sanitizeHTML(s) {
    return s
        .replace(/<(?!\/?(?:b|i|u|strike|ol|ul|li)\b)[^>]+>/g, "") //bye bye tags (no <script>)
        .replace(/(?<=<[^>]*)\w+=["\w]+/g, ""); //bye bye attributes (no onload=)
}

function attachFileListener(x) {
    if (window.androidMuffinComms) {
        x.addEventListener("click", async function () {
            app.dialog.preloader("Loading...");
            let result = await MuffinComms.send(x.dataset.overrideCommsCall || "inputFile", null, responseType = "json");
            app.dialog.close();
            const dataTransfer = new DataTransfer();
            dataTransfer.items.add(new File([MuffinComms.deserialize(result.file, Uint8Array)], result.name));
            x.files = dataTransfer.files;
            x.dispatchEvent(new Event("change"));
        });
    }
}

function onElectronUploadComplete(name, data, recipeID) {
    if (currentRecipe && currentRecipe.id == recipeID && app.popup.get("#recipe-popup").opened) {
        app.dialog.confirm("Would you like to save your changes to the recipe file?", "Update file?", function () {
            updateRecipe({
                id: recipeID,
                file: new File([MuffinComms.deserialize(data, Uint8Array)], name)
            });
        });
    }
}

if (window.androidMuffinComms) {
    document.querySelectorAll("input[type='file']").forEach(attachFileListener);
}

var currentRecipe = null;
var categoryCache = null;

var app = new Framework7({
    el: '#app',
    name: 'Recipes',
});

document.querySelectorAll(".tabs > .tab.view").forEach(function (el) {
    el.addEventListener("tab:show", function () {
        ID.menuList.querySelectorAll(`.item-link:not([href="#${el.id}"])`).forEach(x => x.classList.remove("tab-link-active"));
        ID.menuList.querySelector(`.item-link[href="#${el.id}"]`).classList.add("tab-link-active");
        let tab = el.id.split("-")[1];
        ID.mainTitle.innerText = {
            "home": "Home",
            "search": "Search recipes",
            "categories": "Categories",
            "add": "Create recipe",
        }[tab];
    });
});

function onResize() {
    let size = Math.min(ID.menuCardsContainer.parentNode.offsetWidth, ID.menuCardsContainer.parentNode.offsetHeight);
    ID.menuCardsContainer.style.width = size + "px";
    ID.menuCardsContainer.style.height = size + "px";
}
window.addEventListener("resize", onResize);
onResize();
