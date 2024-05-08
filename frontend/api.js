const API_DESTINATION = "https://recipes.rectanglered.com/api/";
// const API_DESTINATION = "https://recipes.rectanglered.com/backend/RecipeBackend/";

const AuthAPI = {
    async dispatch(route, args) {
        try {
            let fetcher = null;

            fetcher = await fetch(API_DESTINATION + "auth/" + route, {
                method: "POST",
                headers: {
                    'Accept': '*/*'
                },
                body: JSON.stringify(args)
            });

            let response = await fetcher.text();
            try {
                return JSON.parse(response);
            } catch {
                throw "Unknown response format " + response;
            }
        } catch (e) {
            app.dialog.close();
            app.dialog.alert("The operation failed");
            throw e;
        }
    },

    async login(email, password) {
        let response = await AuthAPI.dispatch("login", {
            email: email,
            password: password
        });

        if (response.successful) {
            window.localStorage.setItem("token", response.token);
        }

        return response.successful;
    },

    async registerCode(email, password) {
        return await AuthAPI.dispatch("register/code", {
            email: email,
            password: password
        });
    },
    async registerConfirm(email, code) {
        return await AuthAPI.dispatch("register/confirm", {
            email: email,
            code: code
        });
    },

    async resetCode(email) {
        return await AuthAPI.dispatch("reset/code", {
            email: email
        });
    },
    async resetConfirm(email, password, code) {
        return await AuthAPI.dispatch("reset/confirm", {
            email: email,
            password: password,
            code: code
        });
    },

    async validToken(token) {
        try {
            let fetcher = await fetch(API_DESTINATION + "auth/token", {
                method: "GET",
                headers: {
                    'Accept': '*/*',
                    "Authorization": "Bearer " + token
                }
            });

            let response = await fetcher.text();
            try {
                return JSON.parse(response).valid;
            } catch {
                throw "Unknown response format";
            }
        } catch (e) {
            console.error(e);
            return new Promise(function (resolve, reject) {
                setTimeout(async function () {
                    resolve(await AuthAPI.validToken(token));
                }, 1000);
            });
        }
    }
};

const RecipeAPI = {
    async dispatchCore(route, method, args) {
        try {
            let token = window.localStorage.getItem("token");
            if (!await AuthAPI.validToken(token)) {
                throw "Invalid token";
            }
            const headers = {
                'Accept': '*/*',
                "Authorization": "Bearer " + token
            };

            if (method === "GET") {
                return await fetch(API_DESTINATION + "recipe/" + route + "?" + new URLSearchParams(args).toString(), {
                    method: method,
                    headers: headers
                });
            } else if (method === "POST") {
                let form = new FormData();
                for (const [name, value] of Object.entries(args)) {
                    form.append(name, value);
                }
                return await fetch(API_DESTINATION + "recipe/" + route, {
                    method: method,
                    headers: headers,
                    body: form
                });
            } else {
                throw `Invalid method '${method}'`;
            }
        } catch (e) {
            console.error(e);
            app.dialog.close();
            app.dialog.alert("The operation failed");
            throw e;
        }
    },

    async dispatch(route, method, args) {
        try {
            let json = await RecipeAPI.dispatchCore(route, method, args).then(x => x.json());
            if (!json.successful) {
                console.log(json);
                app.dialog.close();
                app.dialog.alert("The operation failed");
            }
            return json;
        } catch (e) {
            console.error(e);
            app.dialog.close();
            app.dialog.alert("The operation failed");
        }
    },

    async create(title, description, image, type, fileOrUrl) {
        let args = {
            title: title,
            description: description,
            image: image
        }

        if (["url", "file"].includes(type)) {
            args[type] = fileOrUrl;
        } else {
            throw `Invalid type '${type}'`;
        }

        return await RecipeAPI.dispatch("create", "POST", args);
    },

    async update(options) {
        return await RecipeAPI.dispatch("update", "POST", options);
    },

    async get(id) {
        return await RecipeAPI.dispatch("get", "GET", { id: id });
    },

    async search(q) {
        return (await RecipeAPI.dispatch("search", "GET", { q: q })).recipes;
    },

    async image(id) {
        let response = await RecipeAPI.dispatchCore("image", "GET", { id: id });
        if (!response.ok || response.headers.get("Content-Length") == 0) return "no_image.png";
        let ct = response.headers.get("Content-Type");
        let blob = new Blob([await response.blob()], { type: ct });
        return URL.createObjectURL(blob);
    },

    async file(id) {
        return await RecipeAPI.dispatchCore("file", "GET", { id: id });
    },

    async delete(id) {
        return await RecipeAPI.dispatch("delete", "POST", { id: id });
    }
};

const CategoryAPI = {
    
    async dispatchCore(route, method, args) {
        try {
            let token = window.localStorage.getItem("token");
            if (!await AuthAPI.validToken(token)) {
                throw "Invalid token";
            }
            const headers = {
                'Accept': '*/*',
                "Authorization": "Bearer " + token
            };

            if (method === "GET") {
                return await fetch(API_DESTINATION + "category/" + route + "?" + new URLSearchParams(args).toString(), {
                    method: method,
                    headers: headers
                });
            } else if (method === "POST") {
                let form = new FormData();
                for (const [name, value] of Object.entries(args)) {
                    form.append(name, value);
                }
                return await fetch(API_DESTINATION + "category/" + route, {
                    method: method,
                    headers: headers,
                    body: form
                });
            } else {
                throw `Invalid method '${method}'`;
            }
        } catch (e) {
            console.error(e);
            app.dialog.close();
            app.dialog.alert("The operation failed");
            throw e;
        }
    },

    async dispatch(route, method, args) {
        try {
            let json = await CategoryAPI.dispatchCore(route, method, args).then(x => x.json());
            if (!json.successful) {
                console.log(json);
                app.dialog.close();
                app.dialog.alert("The operation failed");
            }
            return json;
        } catch (e) {
            console.error(e);
            app.dialog.close();
            app.dialog.alert("The operation failed");
        }
    },

    async list() {
        return await CategoryAPI.dispatch("list", "GET", {});
    },

    async create(name, color) {
        return await CategoryAPI.dispatch("create", "POST", { name: name, color: color });
    },

    async delete(id) {
        return await CategoryAPI.dispatch("delete", "POST", { id: id });
    },
    
    async assign(recipe, category) {
        return await CategoryAPI.dispatch("assign", "POST", { recipe: recipe, category: category })
    },

    async recipe(id) {
        return await CategoryAPI.dispatch("recipe", "GET", { id: id });
    },

    async set(recipe, categories) {
        return await CategoryAPI.dispatch("set", "POST", { id: recipe, categories: categories.join(",") })
    },

    async edit(id, name, color) {
        return await CategoryAPI.dispatch("edit", "POST", { id: id, name: name, color: color })
    }
}
