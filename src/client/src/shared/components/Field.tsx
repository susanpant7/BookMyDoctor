import type { InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from 'react'

const controlClass =
  'mt-1.5 block w-full rounded-lg border-0 bg-white px-3 py-2.5 text-sm text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-inset focus:ring-indigo-600 disabled:bg-slate-100 disabled:text-slate-500'

function FieldLabel({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block text-sm font-medium text-slate-700">
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
  ...props
}: { label: string } & TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <FieldLabel label={label}>
      <textarea className={controlClass} {...props} />
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
