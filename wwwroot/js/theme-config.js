tailwind.config = {
    darkMode: "class",
    theme: {
        extend: {
            colors: {
                primary: "#6366f1", // Indigo
                "background-light": "#F3F4F6", // Gray 100
                "surface-light": "#FFFFFF",
                "background-dark": "#181824", // Deep dark purple/gray
                "surface-dark": "#222232", // Slightly lighter for cards
                "accent-yellow": "#FDBF5E",
                "accent-blue": "#5D87FF",
                "accent-teal": "#2CD9C5",
                "accent-pink": "#FF6B8B",
                "glass": "rgba(255, 255, 255, 0.25)",
                "glass-dark": "rgba(30, 30, 45, 0.35)",
                "glass-border": "rgba(255, 255, 255, 0.1)",
            },
            fontFamily: {
                display: ["Inter", "sans-serif"],
            },
            borderRadius: {
                DEFAULT: "1rem",
            },
        },
    },
};
