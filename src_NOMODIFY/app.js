function formatDate(s) {
    return new Date(s).toLocaleString("en-GB", { dateStyle: "short" });
}

function sanitizeHTML(s) {
    return s
        .replace(/<(?!\/?(?:b|i|u|strike|ol|ul|li)\b)[^>]+>/g, "") //bye bye tags (no <script>)
        .replace(/(?<=<[^>]*)\w+=["\w]+/g, ""); //bye bye attributes (no onload=)
}

var currentRecipe = null;
async function openRecipe(id, animate=true) {
    if (!await checkToken()) {
        return;
    }
    let [file, recipe] = await Promise.all([RecipeAPI.file(id), RecipeAPI.get(id)]);
    if (!recipe.successful) {
        return;
    }

    let ct = file.headers.get("Content-Type");
    currentRecipe = recipe;
    currentRecipe.id = id;

    app.popup.close("#recipe-popup", animate);
    app.popup.open("#recipe-popup", animate);

    ID.recipeFileContainer.innerHTML = "";
    ID.recipeInfo.innerText = "Created: " + formatDate(recipe.created) + ", Modified: " + formatDate(recipe.modified);
    ID.recipeTitle.innerText = recipe.title;
    ID.recipeTitleTop.innerText = recipe.title;
    ID.recipeDescription.innerHTML = sanitizeHTML(recipe.description);
    RecipeAPI.image(id).then(url => ID.recipeImage.src = url);

    let elButtonBar = document.createElement("div");
    elButtonBar.className = "recipe-button-bar";

    let elButtonOpen = document.createElement("a");
    elButtonOpen.className = "button button-active external";
    elButtonOpen.target = "_blank";
    elButtonOpen.innerHTML = "<i class='icon material-icons'>open_in_new</i> Open in new tab";
    elButtonBar.appendChild(elButtonOpen);

    let elButtonDownload = document.createElement("a");
    elButtonDownload.className = "button button-active external";
    elButtonDownload.target = "_self";
    elButtonDownload.download = recipe.title;
    elButtonDownload.innerHTML = "<i class='icon material-icons'>download</i> Download";
    elButtonBar.appendChild(elButtonDownload);

    let elInputReplace = document.createElement("input");
    elInputReplace.hidden = true;
    elInputReplace.type = "file";
    elInputReplace.addEventListener("change", function() {
        if (elInputReplace.files.length > 0) {
            app.dialog.confirm(`Are you sure you would like to change the selected recipe file to '${elInputReplace.files[0]?.name}'? This cannot be undone!`, "Update file?", function() {

            });
        }
    });
    elButtonBar.appendChild(elInputReplace);

    let elButtonReplace = document.createElement("button");
    elButtonReplace.className = "button button-active";
    elButtonReplace.innerHTML = "<i class='icon material-icons'>upload</i> Replace";
    elButtonReplace.addEventListener("click", function() {
        elInputReplace.click();
    });
    elButtonBar.appendChild(elButtonReplace);

    ID.recipeFileContainer.appendChild(elButtonBar);

    if (window.electronAPI) {
        let elButtonEdit = document.createElement("button");
        elButtonEdit.className = "button button-active";
        elButtonEdit.innerHTML = "<i class='icon material-icons'>edit</i> Edit";
        elButtonBar.appendChild(elButtonReplace);
    }

    let blob = new Blob([await file.blob()], { type: ct });
    let url = URL.createObjectURL(blob);
    elButtonDownload.href = url;
    elButtonOpen.href = url;

    if (ct == "application/pdf") {
        let elEmbed = document.createElement("embed");
        elEmbed.src = url;
        elEmbed.className = "pdf-doc";
        elEmbed.style.marginTop = "8px";

        ID.recipeFileContainer.appendChild(elEmbed);
    } else { //probably word
        let elContainer = document.createElement("div");
        elContainer.className = "word-doc";
        elContainer.style.marginTop = "8px";

        let result = await mammoth.convertToHtml({ arrayBuffer: blob.arrayBuffer() });
        elContainer.innerHTML = result.value;

        ID.recipeFileContainer.appendChild(elContainer);
    }
}

function populateSearchResults(results) {
    ID.searchResults.innerHTML = "";

    for (let result of results) {
        let elSearchResult = document.createElement("li");
        elSearchResult.innerHTML = `
            <li>
                <a class="item-link">
                    <div class="item-content">
                        <div class="item-media"><img style="border-radius: 8px"
                                width="70" />
                        </div>
                        <div class="item-inner">
                            <div class="item-title-row">
                                <div class="item-title"></div>
                            </div>
                            <div class="item-subtitle"></div>
                            <div class="item-text"></div>
                        </div>
                    </div>
                </a>
            </li>
        `;
        elSearchResult.querySelector(".item-title").innerText = result.title;
        RecipeAPI.image(result.id).then(url => elSearchResult.querySelector(".item-media").children[0].src = url);
        elSearchResult.querySelector(".item-subtitle").innerText = "Last modified: " + formatDate(result.modified);
        elSearchResult.querySelector(".item-text").innerHTML = sanitizeHTML(result.hint);
        elSearchResult.addEventListener("click", function () {
            openRecipe(result.id);
        });
        ID.searchResults.appendChild(elSearchResult);
    }
}

var app = new Framework7({
    // App root element
    el: '#app',
    // App Name
    name: 'Recipes',
    // Enable swipe panel
    // panel: {
    //     swipe: true,
    // },
    // ... other parameters
});

app.textEditor.create({
    el: '#recipe-description-editor-create',
    buttons: [["bold", "italic", "underline", "strikeThrough"], ["orderedList", "unorderedList"]]
});

app.textEditor.create({
    el: '#recipe-description-editor-edit',
    buttons: [["bold", "italic", "underline", "strikeThrough"], ["orderedList", "unorderedList"]]
});

ID.recipeDescriptionEditorEdit.addEventListener("click", function() {
    setTimeout(function() {
        ID.editDescription.focus();
    }, 100);
});

ID.buttonCreateRecipe.addEventListener("click", async function () {
    app.dialog.preloader("Creating...");
    if (!await checkToken()) {
        return;
    }
    let type = ID.uploadFile.classList.contains("tab-active") ? "file" : "url";
    await RecipeAPI.create(ID.createTitle.value, ID.createDescription.innerHTML, ID.uploadImageInput.files[0], type, type == "file" ? ID.uploadFileInput.files[0] : ID.createUrl.value);
    app.dialog.close();
});

ID.uploadFileInput.addEventListener("change", function () {
    if (ID.uploadFileInput.files.length > 0) {
        ID.uploadFileButton.innerText = ID.uploadFileInput.files[0].name;
    }
});

ID.uploadFileButton.addEventListener("click", function () {
    ID.uploadFileInput.click();
});

ID.uploadImageInput.addEventListener("change", function () {
    if (ID.uploadImageInput.files.length > 0) {
        var fr = new FileReader();
        fr.onload = function () {
            ID.uploadImagePreview.src = fr.result;
        }
        fr.readAsDataURL(ID.uploadImageInput.files[0]);
    }
});

ID.uploadImageButton.addEventListener("click", function () {
    ID.uploadImageInput.click();
});

ID.recipeButtonEdit.addEventListener("click", async function () {
    ID.recipeViewDetails.style.opacity = "0";
    setTimeout(function () {
        ID.recipeViewDetails.style.display = "none";
        ID.recipeViewEdit.style.display = "block";
        setTimeout(function () {
            ID.recipeViewEdit.style.opacity = "1";
        }, 50);
    }, 500);

    ID.editTitle.value = currentRecipe.title;
    ID.editTitle.dispatchEvent(new Event("input"));

    ID.editDescription.innerHTML = sanitizeHTML(currentRecipe.description);
    ID.editDescription.dispatchEvent(new Event("input"));
});

ID.recipeButtonSave.addEventListener("click", async function () {
    app.dialog.preloader("Updating...");
    if (!await checkToken()) {
        return;
    }

    RecipeAPI.update({
        id: currentRecipe.id,
        title: ID.editTitle.value,
        description: ID.editDescription.innerHTML
    });

    ID.recipeViewEdit.style.opacity = "0";
    setTimeout(function () {
        ID.recipeViewEdit.style.display = "none";
        ID.recipeViewDetails.style.display = "block";
        setTimeout(function () {
            ID.recipeViewDetails.style.opacity = "1";
        }, 50);
    }, 500);

    await openRecipe(currentRecipe.id, false);

    app.dialog.close();
});

ID.recipeButtonDelete.addEventListener("click", async function () {
    if (!await checkToken()) {
        return;
    }
    app.dialog.confirm("Really delete recipe " + currentRecipe.title + " forever? (This cannot be undone!)", "Delete", async function () {
        if (!await checkToken()) {
            return;
        }
        app.dialog.preloader("Deleting...");
        let result = await RecipeAPI.delete(currentRecipe.id);
        if (result.successful) { //dont close the error message
            app.dialog.close();
            app.popup.close();
        }
    });
})

ID.buttonSearch.addEventListener("click", async function () {
    app.dialog.preloader("Searching...");
    if (!await checkToken()) {
        return;
    }

    let results = await RecipeAPI.search(ID.inputSearch.value);
    app.dialog.close();
    populateSearchResults(results);
});
