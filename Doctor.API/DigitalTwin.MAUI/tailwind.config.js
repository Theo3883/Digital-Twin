/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Components/**/*.{razor,html,cshtml}",
    "./Pages/**/*.{razor,html,cshtml}",
    "./wwwroot/index.html"
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        'pango-dark': '#0a0a0a',
        'pango-card': '#1a1a1a',
      },
    },
  },
  plugins: [],
}
