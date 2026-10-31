import type { ButtonHTMLAttributes } from 'react'
import { buttonClass, type ButtonSize, type ButtonVariant } from '@/shared/components/buttonStyles'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  size?: ButtonSize
}

export default function Button({ variant, size, className = '', type = 'button', ...props }: ButtonProps) {
  return <button type={type} className={`${buttonClass(variant, size)} ${className}`} {...props} />
}
