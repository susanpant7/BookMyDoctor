const base =
  'inline-flex items-center justify-center gap-2 rounded-lg font-semibold transition focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60'

const variants = {
  primary: 'bg-indigo-600 text-white shadow-sm hover:bg-indigo-500',
  secondary: 'bg-white text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50',
  danger: 'bg-red-600 text-white shadow-sm hover:bg-red-500',
  ghost: 'text-slate-600 hover:bg-slate-100 hover:text-slate-900',
  dangerGhost: 'text-red-600 hover:bg-red-50 hover:text-red-700',
}

const sizes = {
  sm: 'px-3 py-1.5 text-sm',
  md: 'px-4 py-2.5 text-sm',
  lg: 'px-6 py-3 text-base',
}

export type ButtonVariant = keyof typeof variants
export type ButtonSize = keyof typeof sizes

export function buttonClass(variant: ButtonVariant = 'primary', size: ButtonSize = 'md') {
  return `${base} ${variants[variant]} ${sizes[size]}`
}
