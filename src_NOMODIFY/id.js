const ID = new Proxy({}, {
    get(object, prop) {
        let id = prop.replace(/([A-Z])/g, (_, a) => "-" + a.toLowerCase());
        if (!object[id]) {
            object[id] = document.getElementById(id);
        }
        return object[id];
    }
});

const CLASS = new Proxy({}, {
    get(_, prop) {
        let className = prop.replace(/([A-Z])/g, (_, a) => "-" + a.toLowerCase());
        let elements = [...document.getElementsByClassName(className)];
        
        let handler = {
            set(_, prop, value) {
                for (let index = 0; index < elements.length; index++) {
                    elements[index][prop] = value;
                }
                return true;
            },
            get(_, prop) {
                if (typeof elements[0][prop] === "function") {
                    return function() {
                        for (let index = 0; index < elements.length; index++) {
                            elements[index][prop](...arguments);
                        }
                    };
                } else {
                    for (let index = 0; index < elements.length; index++) {
                        elements[index] = elements[index][prop];
                        if (elements[index] === undefined) {
                            throw new Error(`Not all elements have member '${prop}' (assertion failed on element ${index})`)
                        }
                    }
                    return new Proxy({}, handler);
                }
            }
        };
        return new Proxy({}, handler);
    }
});
