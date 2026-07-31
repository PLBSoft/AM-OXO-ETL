// Dark/light theme switch, backed by Bootstrap 5.3's [data-bs-theme] attribute on <html> -- see
// wwwroot/css/theme-m3.css, which already defines the full M3 dark palette under
// [data-bs-theme="dark"] and needed no change for this feature. Plain global script, not a Blazor
// JS module or IJSRuntime interop: the toggle button (NavMenu.razor) uses a native `onclick`, so it
// works without a circuit round trip, matching NavMenu.razor's own existing
// `onclick="document.querySelector('.navbar-toggler').click()"` convention for the mobile
// hamburger toggle.
//
// This file is referenced as the very first element inside <head> (App.razor), before any
// stylesheet, and executes synchronously (no `defer`/`async`/`type="module"`) so the correct
// [data-bs-theme] attribute is set before first paint -- avoiding a light-then-dark flash for a
// visitor who has already chosen (or whose OS prefers) dark mode.
window.amOxoTheme = (function () {
    var STORAGE_KEY = "am-oxo-theme";

    function preferredSystemTheme() {
        return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches
            ? "dark"
            : "light";
    }

    function get() {
        return document.documentElement.getAttribute("data-bs-theme") || "light";
    }

    function set(theme) {
        document.documentElement.setAttribute("data-bs-theme", theme);
        window.localStorage.setItem(STORAGE_KEY, theme);
    }

    function toggle() {
        set(get() === "dark" ? "light" : "dark");
    }

    // Applies the stored choice, or the OS preference when the visitor has never toggled the
    // switch explicitly -- never persists this initial choice to localStorage on its own, so a
    // later OS-level preference change keeps being honored until the visitor makes an explicit
    // choice via toggle()/set().
    function apply() {
        var stored = window.localStorage.getItem(STORAGE_KEY);
        document.documentElement.setAttribute("data-bs-theme", stored || preferredSystemTheme());
    }

    return { get: get, set: set, toggle: toggle, apply: apply };
})();

window.amOxoTheme.apply();
