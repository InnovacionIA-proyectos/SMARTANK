/**
 * Color basado en una variable CSS que admite el modificador de opacidad de Tailwind
 * (`bg-danger/10`, `hover:bg-border/40`). Con `var(--x)` a secas Tailwind 3 descarta
 * esas utilidades sin avisar, y los tintes de badges, alertas y hovers del design
 * system nunca llegarían a la hoja de estilos. `color-mix` mantiene el tema
 * claro/oscuro: la mezcla se resuelve en el navegador con el valor vigente del token.
 */
const token =
  (variable) =>
  ({ opacityValue }) =>
    // Sin modificador de opacidad (o con la variable interna de Tailwind) se deja el
    // color plano: así solo las utilidades con tinte dependen de `color-mix`.
    opacityValue === undefined || String(opacityValue).startsWith('var(')
      ? `var(${variable})`
      : `color-mix(in srgb, var(${variable}) calc(${opacityValue} * 100%), transparent)`;

/**
 * Tokens semánticos del design system (docs/design_system.md).
 * Los colores se resuelven vía CSS variables definidas en src/styles.scss,
 * de modo que el modo claro/oscuro conmuta con la clase `.dark` en <html>.
 *
 * @type {import('tailwindcss').Config}
 */
module.exports = {
  darkMode: 'class',
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      colors: {
        primary: {
          DEFAULT: token('--color-primary'),
          hover: token('--color-primary-hover'),
        },
        secondary: token('--color-secondary'),
        accent: token('--color-accent'),
        bg: token('--color-bg'),
        surface: token('--color-surface'),
        border: token('--color-border'),
        'text-primary': token('--color-text-primary'),
        'text-muted': token('--color-text-muted'),
        success: token('--color-success'),
        warning: token('--color-warning'),
        danger: token('--color-danger'),
        info: token('--color-info'),
      },
      fontFamily: {
        sans: ['var(--font-inter)', 'system-ui', 'Segoe UI', 'Roboto', 'sans-serif'],
      },
      fontSize: {
        // Escala tipográfica del design system (tamaño / line-height)
        h1: ['28px', { lineHeight: '34px', fontWeight: '600' }],
        h2: ['22px', { lineHeight: '28px', fontWeight: '600' }],
        h3: ['18px', { lineHeight: '24px', fontWeight: '600' }],
        body: ['14px', { lineHeight: '20px' }],
        caption: ['12px', { lineHeight: '16px' }],
        metric: ['32px', { lineHeight: '36px', fontWeight: '700' }],
      },
      spacing: {
        // Escala 4·8·12·16·24·32·48·64 (los múltiplos no-default)
        18: '4.5rem',
      },
    },
  },
  plugins: [],
};
