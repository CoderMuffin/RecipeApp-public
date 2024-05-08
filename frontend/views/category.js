var currentCategoryID = null;
var categoryColor = [
    { bg: "red", fg: "white"},
    { bg: "orange", fg: "black" },
    { bg: "yellow", fg: "black" },
    { bg: "green", fg: "black" },
    { bg: "blue", fg: "white" },
    { bg: "deeppurple", fg: "white" },
    { bg: "pink", fg: "white" }
]
async function refreshCategories() {
    app.dialog.preloader("Refreshing...");
    if (!await checkToken()) {
        return;
    }
    let categories = await CategoryAPI.list();
    ID.categoryList.innerHTML = "";
    if (categories.categories.length == 0) {
        let elEntry = document.createElement("li");
        elEntry.className = "list-empty-hint";
        elEntry.innerHTML = `
            You have no categories! <button class="button button-active" onclick="categoryCreateDialog.open();" href="#view-add">Create new</button>
        `;
        ID.categoryList.appendChild(elEntry);
    }
    for (let category of categories.categories) {
        let elEntry = document.createElement("li");
        elEntry.className = "item-content item-inner";
        elEntry.style = "cursor: pointer";
        elEntry.innerHTML = `
          <div class="category-text"></div>
          <div style="display: flex; flex-direction: row; cursor: auto">
            <button class="button"> <!--event bubbles, no need to register listener-->
                <i class="icon material-icons">search</i>
            </button>
            <button class="button category-button-edit">
                <i class="icon material-icons">edit</i>
            </button>
            <button class="button color-red category-button-delete">
                <i class="icon material-icons">delete</i>
            </button>
          </div>
        `;
        elEntry.addEventListener("click", function () {
            app.tab.show("#view-search");
            ID.inputSearch.value = "[" + category.name + "]";
            searchRecipes();
        });
        elEntry.querySelector(".category-button-delete").addEventListener("click", function (event) {
            event.stopPropagation();
            app.dialog.confirm("Really delete category " + category.name + " forever and remove the category from all recipes? (This cannot be undone!)", "Delete", async function () {
                if (!await checkToken()) {
                    return;
                }
                app.dialog.preloader("Deleting...");
                let result = await CategoryAPI.delete(category.id);
                if (result.successful) { //dont close the error message
                    app.dialog.close();
                    app.popup.close();
                    refreshCategories();
                }
            });
        });
        elEntry.querySelector(".category-button-edit").addEventListener("click", function (event) {
            event.stopPropagation();

            categoryEditDialog.open();
            currentCategoryID = category.id;

            let elName = categoryEditDialog.el.querySelector("input[type='text']")
            elName.value = category.name;
            elName.dispatchEvent(new Event("input"));
            categoryEditDialogSmartSelect.setValue(category.color);
        });
        elEntry.querySelector(".category-text").innerText = category.name;
        elEntry.querySelector(".category-text").appendChild(EL("span", { className: "color-widget bg-color-" + categoryColor[category.color].bg }));

        ID.categoryList.appendChild(elEntry);
    }
    app.dialog.close();
}

function createCategoryElement(category) {
    return EL("span", {
        innerText: category.name,
        className: `recipe-tag bg-color-${categoryColor[category.color].bg} color-${categoryColor[category.color].fg}`
    });
}

function createCategoryListElement(categories) {
    return EL("span", {
        className: "recipe-tag-list",
        children: categories.map(category => createCategoryElement(category))
    });
}

const categoryDialogContent = `
    <div class="list">
        <ul>
            <li>
                <div class="item-content item-input item-input-outline">
                    <div class="item-inner">
                        <div style="background-color: var(--f7-dialog-bg-color)" class="item-title item-floating-label">Name</div>
                        <div class="item-input-wrap">
                            <input type="text" placeholder="Name..." />
                        </div>
                    </div>
                </div>
            </li>
            <li>
                <a class="item-link smart-select" data-open-in="popover">
                    <select>
                        <option data-option-class="color-red" value="0" selected>Red</option>
                        <option data-option-class="color-orange" value="1">Orange</option>
                        <option data-option-class="color-yellow" value="2">Yellow</option>
                        <option data-option-class="color-green" value="3">Green</option>
                        <option data-option-class="color-blue" value="4">Blue</option>
                        <option data-option-class="color-deeppurple" value="5">Purple</option>
                        <option data-option-class="color-purple" value="6">Pink</option>
                    </select>
                    <div class="item-content">
                        <div class="item-inner">
                            <div class="item-title">Color</div>
                        </div>
                    </div>
                </a>
            </li>
        </ul>
    </div>
`;

var categoryCreateDialog = app.dialog.create({
    content: categoryDialogContent,
    title: "Create category",
    closeByBackdropClick: true,
    routableModals: false,
    buttons: [
        {
            text: "Cancel",
            close: true
        },
        {
            text: "Create",
            onClick: async function (dialog) {
                app.dialog.close();
                app.dialog.preloader("Creating...");
                if (!await checkToken()) {
                    return;
                }

                await CategoryAPI.create(dialog.el.querySelector("input[type='text']").value, dialog.el.querySelector("select").value);

                app.dialog.close();

                refreshCategories();
            }
        }
    ]
});

var categoryEditDialog = app.dialog.create({
    content: categoryDialogContent,
    title: "Edit category",
    closeByBackdropClick: true,
    routableModals: false,
    buttons: [
        {
            text: "Cancel",
            close: true
        },
        {
            text: "Save",
            onClick: async function (dialog) {
                app.dialog.close();
                app.dialog.preloader("Updating...");
                if (!await checkToken()) {
                    return;
                }

                await CategoryAPI.edit(currentCategoryID, dialog.el.querySelector("input[type='text']").value, dialog.el.querySelector("select").value);

                app.dialog.close();

                refreshCategories();
            }
        }
    ]
});

app.smartSelect.create({ view: app.views.get("#view-categories"), el: categoryCreateDialog.el.querySelector("a.smart-select"), openIn: "popover" });
var categoryEditDialogSmartSelect = app.smartSelect.create({ view: app.views.get("#view-categories"), el: categoryEditDialog.el.querySelector("a.smart-select"), openIn: "popover" });

ID.fabNewCategory.addEventListener("click", function () {
    categoryCreateDialog.open();
});

ID.viewCategories.addEventListener("tab:show", function () {
    refreshCategories();
});

ID.recipeTagSelect.addEventListener("change", function () {
    let selected = Array.from(ID.recipeTagSelect.options).filter(option => option.selected);
    CategoryAPI.set(currentRecipe.id, selected.map(option => option.value));
    updateTagText(categoryCache.filter(x => selected.some(s => s.value == x.id)));
});
