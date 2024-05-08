async function openRecipe(id, animate = true) {
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
    if (recipe.type == "url") {
        let elLink = document.createElement("a");
        elLink.target = "_blank";
        elLink.href = recipe.url;
        elLink.className = "item-link external";
        elLink.innerText = recipe.url;

        ID.recipeInfo.innerText += "\nOriginal URL: ";
        ID.recipeInfo.appendChild(elLink);
    }
    ID.recipeTitle.innerText = recipe.title;
    ID.recipeTitleTop.innerText = recipe.title;
    ID.recipeDescription.innerHTML = sanitizeHTML(recipe.description);
    RecipeAPI.image(id).then(function(url) {
        if (url !== "no_image.png") {
            ID.recipeImage.src = url;
        } else {
            ID.recipeImage.removeAttribute("src");
        }
    });

    let blob = new Blob([await file.blob()], { type: ct });
    let url = URL.createObjectURL(blob);

    let elButtonBar = document.createElement("div");
    elButtonBar.className = "recipe-button-bar";

    let elButtonOpen = document.createElement("a");
    elButtonOpen.className = "button button-active external";
    if (window.androidMuffinComms) {
        elButtonOpen.innerHTML = `<i class='icon material-icons'>share</i> Share`;
        elButtonOpen.href = "#";
        elButtonOpen.addEventListener("click", async function () {
            app.dialog.preloader("Downloading...");
            await MuffinComms.send("shareFile", {
                bytes: MuffinComms.serialize(await blob.arrayBuffer()),
                ct: ct
            });
            app.dialog.close();
        });
    } else if (window.electronAPI) {
        elButtonOpen.addEventListener("click", async function () {
            window.electronAPI.download(ct, await blob.arrayBuffer(), id);
        });
        elButtonOpen.href = "#";
        elButtonOpen.innerHTML = `<i class='icon material-icons'>edit</i> Edit`;
    } else {
        elButtonOpen.target = "_blank";
        elButtonOpen.href = url;
        elButtonOpen.innerHTML = `<i class='icon material-icons'>open_in_new</i> Open`;
    }
    elButtonBar.appendChild(elButtonOpen);

    let elButtonDownload = document.createElement("a");
    elButtonDownload.className = "button button-active external";
    elButtonDownload.innerHTML = "<i class='icon material-icons'>download</i> Download";
    if (window.androidMuffinComms) {
        elButtonDownload.addEventListener("click", async function () {
            app.dialog.preloader("Downloading...");
            await MuffinComms.send("saveFile", {
                bytes: MuffinComms.serialize(await blob.arrayBuffer()),
                ct: ct
            });
            app.dialog.close();
        });
    } else {
        elButtonDownload.target = "_self";
        elButtonDownload.download = recipe.title;
        elButtonDownload.href = url;
    }
    elButtonBar.appendChild(elButtonDownload);

    let elInputReplace = document.createElement("input");
    elInputReplace.hidden = true;
    elInputReplace.type = "file";
    attachFileListener(elInputReplace);
    elInputReplace.addEventListener("change", function () {
        if (elInputReplace.files.length > 0) {
            app.dialog.confirm(`Are you sure you would like to change the selected recipe file to '${elInputReplace.files[0]?.name}'? This cannot be undone!`, "Update file?", function () {
                updateRecipe({
                    id: currentRecipe.id,
                    file: elInputReplace.files[0]
                });
            });
        }
    });
    elButtonBar.appendChild(elInputReplace);

    let elButtonReplace = document.createElement("button");
    elButtonReplace.className = "button button-active";
    elButtonReplace.innerHTML = "<i class='icon material-icons'>upload</i> Replace";
    elButtonReplace.addEventListener("click", async function () {
        elInputReplace.click();
    });
    elButtonBar.appendChild(elButtonReplace);

    ID.recipeFileContainer.appendChild(elButtonBar);

    try {
        if (ct == "application/pdf") {
            let elEmbed = document.createElement("iframe");
            elEmbed.src = "ext/mobile-viewer/viewer.html";
            elEmbed.className = "pdf-doc";
            elEmbed.style.marginTop = "8px";
            elEmbed.addEventListener("load", function () {
                elEmbed.contentWindow.loadPDF(blob);
            });

            ID.recipeFileContainer.appendChild(elEmbed);
        } else if (ct == "application/vnd.openxmlformats-officedocument.wordprocessingml.document" || ct == "application/msword") {
            let elContainer = document.createElement("div");
            elContainer.className = "word-doc";
            elContainer.style.marginTop = "8px";

            let result = await mammoth.convertToHtml({ arrayBuffer: blob.arrayBuffer() });
            elContainer.innerHTML = result.value;

            ID.recipeFileContainer.appendChild(elContainer);
        } else {
            let elContainer = document.createElement("div");
            elContainer.className = "word-doc";
            elContainer.style.marginTop = "8px";
            elContainer.innerHTML = `
                <div style="height: 100%; width: 100%; display: flex; align-items: center; justify-content: center; flex-direction: column">
                    <i style="font-size: 5em" class="color-yellow icon material-icons">warning</i>
                    <p style="font-size: 3em; margin: 0">Unknown file type</p>
                    <p style="margin: 0">Cannot read Content-Type <code style="color: var(--f7-md-primary)"></code></p>
                </div>
            `;
            elContainer.querySelector("code").innerText = ct;

            ID.recipeFileContainer.appendChild(elContainer);
        }
    } catch (e) {
        let elContainer = document.createElement("div");
        elContainer.className = "word-doc";
        elContainer.style.marginTop = "8px";
        elContainer.innerHTML = `
            <div style="height: 100%; width: 100%; display: flex; align-items: center; justify-content: center; flex-direction: column">
                <i style="font-size: 5em" class="color-yellow icon material-icons">warning</i>
                <p style="font-size: 3em; margin: 0">Something went wrong</p>
                <p style="margin: 0">An error occured when trying to read the file</p>
            </div>
        `;
        console.error(e);

        ID.recipeFileContainer.appendChild(elContainer);
    }

    let [recipeCategories, categories] = await Promise.all([CategoryAPI.recipe(id), CategoryAPI.list()]);
    ID.recipeTagSelect.innerHTML = "";
    categoryCache = categories.categories;
    for (var category of categoryCache) {
        let option = document.createElement("option");
        option.innerText = category.name;
        option.value = category.id;
        option.setAttribute("data-option-class", "color-" + categoryColor[category.color].bg);
        option.selected = recipeCategories.categories.some(x => category.id == x.id);
        ID.recipeTagSelect.appendChild(option);
    }
    updateTagText(recipeCategories.categories);
}

function updateTagText(categories) {
    ID.recipeTags.innerHTML = "";
    if (categories.length == 0) {
        ID.recipeTags.innerText = "None";
        return;
    }
    ID.recipeTags.appendChild(createCategoryListElement(categories));
}

function cancelEdit() {
    ID.recipeViewEdit.style.opacity = "0";
    setTimeout(function () {
        ID.recipeViewEdit.style.display = "none";
        ID.recipeViewDetails.style.display = "block";
        setTimeout(function () {
            ID.recipeViewDetails.style.opacity = "1";
        }, 50);
    }, 500);
}

async function updateRecipe(details) {
    app.dialog.preloader("Updating...");
    if (!await checkToken()) {
        return;
    }

    await RecipeAPI.update(details);

    cancelEdit();

    await openRecipe(currentRecipe.id, false);

    app.dialog.close();
}


app.popup.create("#recipe-popup", {
    closed: function () {
        cancelEdit();
    }
});

app.textEditor.create({
    el: '#recipe-description-editor-edit',
    buttons: [["bold", "italic", "underline", "strikeThrough"], ["orderedList", "unorderedList"]]
});

ID.recipeDescriptionEditorEdit.addEventListener("click", function () {
    setTimeout(function () {
        ID.editDescription.focus();
    }, 100);
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
    await updateRecipe({
        id: currentRecipe.id,
        title: ID.editTitle.value,
        description: ID.editDescription.innerHTML,
        image: ID.uploadImageInputEdit.files.length > 0 ? ID.uploadImageInputEdit.files[0] : null
    });
    ID.uploadImageInputEdit.value = null;
    ID.uploadImagePreviewEdit.src = "";
});

ID.recipeButtonCancel.addEventListener("click", function () {
    cancelEdit();
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
            searchRecipes();
            app.dialog.close();
            app.popup.close();
        }
    });
})

ID.uploadImageButtonEdit.addEventListener("click", function() {
    ID.uploadImageInputEdit.click();
});

ID.uploadImageInputEdit.addEventListener("change", function() {
    if (ID.uploadImageInputEdit.files.length > 0) {
        var fr = new FileReader();
        fr.onload = function () {
            ID.uploadImagePreviewEdit.src = fr.result;
        }
        fr.readAsDataURL(ID.uploadImageInputEdit.files[0]);
    } else {
        ID.uploadImageInputEdit.removeAttribute("src");
    }
});
