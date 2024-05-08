function populateSearchResults(results) {
    ID.searchResults.innerHTML = "";

    if (results.length == 0) {
        let elSearchResult = document.createElement("li");
        elSearchResult.className = "list-empty-hint";
        elSearchResult.innerHTML = `
            No recipes with that text <a class="tab-link button button-active" href="#view-add">Create new</a>
        `;
        ID.searchResults.appendChild(elSearchResult);
    }
    for (let result of results) {
        let elSearchResult = document.createElement("li");
        elSearchResult.innerHTML = `
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
        `;
        RecipeAPI.image(result.id).then(url => elSearchResult.querySelector(".item-media").children[0].src = url);
        let elItemTitle = elSearchResult.querySelector(".item-title");
        elItemTitle.innerText = result.title;
        elItemTitle.appendChild(createCategoryListElement(result.categories));
        elSearchResult.querySelector(".item-subtitle").innerText = "Last modified: " + formatDate(result.modified);
        elSearchResult.querySelector(".item-text").innerHTML = sanitizeHTML(result.hint);
        elSearchResult.addEventListener("click", function () {
            openRecipe(result.id);
        });
        ID.searchResults.appendChild(elSearchResult);
    }
}

async function searchRecipes() {
    app.dialog.preloader("Searching...");
    if (!await checkToken()) {
        return;
    }

    let results = await RecipeAPI.search(ID.inputSearch.value);
    app.dialog.close();
    console.log(results);
    populateSearchResults(results);
}

ID.buttonSearch.addEventListener("click", async function () {
    searchRecipes();
});

ID.viewSearch.addEventListener("tab:show", async function () {
    searchRecipes();
});

ID.inputSearch.addEventListener("keyup", function(e) {
    if (e.key == "Enter") {
        searchRecipes();
    }
});
