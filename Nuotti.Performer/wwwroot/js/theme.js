// Theme detection and management for Nuotti Performer
window.nuottiTheme = {
    dotNetRef: null,
    mediaQuery: null,

    initialize: function (dotNetReference) {
        this.dotNetRef = dotNetReference;
        
        // Listen for system theme changes
        this.mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
        this.mediaQuery.addEventListener('change', this.handleThemeChange.bind(this));
    },

    handleThemeChange: function (e) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('SystemThemeChanged', e.matches);
        }
    },

    dispose: function () {
        if (this.mediaQuery) {
            this.mediaQuery.removeEventListener('change', this.handleThemeChange.bind(this));
        }
        this.dotNetRef = null;
    }
};

