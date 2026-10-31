import type { InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from 'react'

const controlClass =
  'mt-1.5 block w-full rounded-lg border-0 bg-white px-3 py-2.5 text-sm text-stone-900 shadow-sm ring-1 ring-inset ring-stone-300 placeholder:text-stone-400 focus:outline-none focus:ring-2 focus:ring-inset focus:ring-brand-600 disabled:bg-stone-100 disabled:text-stone-500'

function FieldLabel({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block text-sm font-medium text-stone-700">
      {label}
      {children}
    </label>
  )
}

export function TextField({ label, ...props }: { label: string } & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <FieldLabel label={label}>
      <input className={controlClass} {...props} />
    </FieldLabel>
  )
}

export function TextAreaField({
  label,
  className,
  ...props
}: { label: string } & TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <FieldLabel label={label}>
      <textarea className={className ? `${controlClass} ${className}` : controlClass} {...props} />
    </FieldLabel>
  )
}

export function SelectField({
  label,
  children,
  ...props
}: { label: string } & SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <FieldLabel label={label}>
      <select className={controlClass} {...props}>
        {children}
      </select>
    </FieldLabel>
  )
}
