app.textEditor.create({
    el: '#recipe-description-editor-create',
    buttons: [["bold", "italic", "underline", "strikeThrough"], ["orderedList", "unorderedList"]]
});

ID.buttonCreateRecipe.addEventListener("click", async function () {
    app.dialog.preloader("Creating...");
    if (!await checkToken()) {
        return;
    }
    let type = ID.uploadFile.classList.contains("tab-active") ? "file" : "url";
    if (type == "file" && ID.uploadFileInput.files.length == 0) {
        app.dialog.close();
        app.dialog.alert("Please attach a file or url to your recipe");
        return;
    }

    let result = await RecipeAPI.create(ID.createTitle.value, ID.createDescription.innerHTML, ID.uploadImageInput.files[0], type, type == "file" ? ID.uploadFileInput.files[0] : ID.createUrl.value);

    ID.uploadFileInput.value = "";
    ID.uploadImageInput.value = "";
    ID.uploadFileButton.innerText = "Select file";
    ID.uploadImageButton.innerText = "Select image";
    app.dialog.close();

    if (result.successful) {
        app.tab.show(ID.viewSearch);
        openRecipe(result.id);
    } else {
        app.dialog.alert(result.reason, "An error occurred");
        return;
    }
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
    } else {
        ID.uploadImagePreview.removeAttribute("src");
    }
});

ID.uploadImageButton.addEventListener("click", function () {
    ID.uploadImageInput.click();
});
