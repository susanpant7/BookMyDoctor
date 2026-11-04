export interface Tab {
  id: string
  label: string
  count?: number
}

interface TabsProps {
  tabs: Tab[]
  active: string
  onChange: (id: string) => void
  // Every tab takes an equal share of the full width (for a few tabs), instead of the width of its label.
  fill?: boolean
}

export default function Tabs({ tabs, active, onChange, fill = false }: TabsProps) {
  return (
    <div role="tablist" className={`flex gap-1 overflow-x-auto border-b border-stone-200 ${fill ? 'px-1 sm:px-4' : 'px-4'}`}>
      {tabs.map((tab) => {
        const selected = tab.id === active
        return (
          <button
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={selected}
            onClick={() => onChange(tab.id)}
            className={`-mb-px flex items-center gap-2 whitespace-nowrap border-b-2 py-3 text-sm font-medium transition ${
              fill ? 'flex-1 justify-center px-2 sm:flex-none sm:px-3' : 'px-3'
            } ${
              selected
                ? 'border-brand-600 text-brand-700'
                : 'border-transparent text-stone-500 hover:border-stone-300 hover:text-stone-700'
            }`}
          >
            {tab.label}
            {tab.count !== undefined && (
              <span
                className={`rounded-full px-2 py-0.5 text-xs ${selected ? 'bg-brand-50 text-brand-700' : 'bg-stone-100 text-stone-600'}`}
              >
                {tab.count}
              </span>
            )}
          </button>
        )
      })}
    </div>
  )
}
