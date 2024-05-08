const VIEW_LOGIN = "#login-view";
const VIEW_RESET = "#reset-view";
const VIEW_REGISTER = "#register-view";

async function checkToken() {
    let token = window.localStorage.getItem("token");
    result = await AuthAPI.validToken(token);
    if (!result) {
        app.loginScreen.open(VIEW_LOGIN);
        app.dialog.close();
    }
    return result;
}
checkToken().then(function() {
    ID.preloader.style.opacity = "0";
    ID.preloader.style.pointerEvents = "none";
    setTimeout(function() {
        ID.preloader.style.display = "none";
    }, 600);
    window.dispatchEvent(new Event("resize"));
});

function logout() {
    window.localStorage.setItem("token", "");
    window.location.reload();
}

var registerPhase = "email";
ID.buttonRegister.addEventListener("click", async function() {
    if (registerPhase == "email") {
        let result = await AuthAPI.registerCode(ID.registerEmail.value, ID.registerPassword.value);
        if (!result.successful) {
            app.dialog.alert(result.reason, "Error");
            return;
        }

        ID.registerEmailContainer.classList.add("fadeable-field-hidden");
        ID.registerPasswordContainer.classList.add("fadeable-field-hidden");

        setTimeout(function() {
            ID.registerCodeGuidance.classList.remove("fadeable-field-hidden");
        }, 1000);
        setTimeout(function() {
            ID.registerCodeContainer.classList.remove("fadeable-field-hidden");
        }, 1500);

        registerPhase = "code";
    } else if (registerPhase == "code") {
        let result = await AuthAPI.registerConfirm(ID.registerEmail.value, ID.registerCode.value);
        if (!result.successful) {
            app.dialog.alert(result.reason, "Error");
            return;
        }
        window.location.reload();
    } else {
        throw "Unknown registerPhase";
    }
});

var resetPhase = "email";
ID.buttonReset.addEventListener("click", async function() {
    if (resetPhase == "email") {
        let result = await AuthAPI.resetCode(ID.resetEmail.value);
        if (!result.successful) {
            app.dialog.alert(result.reason, "Error");
            return;
        }

        ID.resetEmailContainer.classList.add("fadeable-field-hidden");
        
        setTimeout(function() {
            ID.resetCodeGuidance.classList.remove("fadeable-field-hidden");
        }, 1000);
        setTimeout(function() {
            ID.resetCodeContainer.classList.remove("fadeable-field-hidden");
        }, 1500);
        setTimeout(function() {
            ID.resetPasswordContainer.classList.remove("fadeable-field-hidden");
        }, 2000);
            
        resetPhase = "code";
    } else if (resetPhase == "code") {
        let result = await AuthAPI.resetConfirm(ID.resetEmail.value, ID.resetPassword.value, ID.resetCode.value);
        if (!result.successful) {
            app.dialog.alert(result.reason, "Error");
            return;
        }
        window.location.reload();
    } else {
        throw "Unknown registerPhase";
    }
});

ID.linkReset.addEventListener("click", async function() {
    app.loginScreen.open(VIEW_RESET);
})
ID.linkRegister.addEventListener("click", async function() {
    app.loginScreen.open(VIEW_REGISTER);
});
CLASS.linkLogin.addEventListener("click", async function() {
    app.loginScreen.close(VIEW_REGISTER);
    app.loginScreen.close(VIEW_RESET);
});

ID.panelMenuLogout.addEventListener("click", function() {
    logout();
});

ID.formLogin.addEventListener("submit", async function(event) {
    event.preventDefault();
    app.dialog.preloader("Logging in...");

    let result = await AuthAPI.login(ID.loginEmail.value, ID.loginPassword.value);
    app.dialog.close();
    if (result) {
        app.loginScreen.close(VIEW_LOGIN);
        app.loginScreen.close(VIEW_RESET);
        app.loginScreen.close(VIEW_REGISTER);
    } else {
        app.dialog.alert("The email or password is incorrect.");
    }
});

window.addEventListener("DOMContentLoaded", function() {
    ID.loginEmail.dispatchEvent(new Event("input"));
    ID.loginPassword.dispatchEvent(new Event("input"));
});
