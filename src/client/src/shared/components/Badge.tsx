const styles = [
  "bg-indigo-50 text-indigo-700",
  "bg-emerald-50 text-emerald-700",
  "bg-amber-50 text-amber-700",
  "bg-sky-50 text-sky-700",
  "bg-rose-50 text-rose-700",
  "bg-violet-50 text-violet-700"
];

// The same name always gets the same colour, so every specialty, including new ones, is coloured without a list of names.
function styleFor(name: string) {
  let hash = 0;
  for (const char of name) hash = (hash * 31 + char.charCodeAt(0)) | 0;
  return styles[Math.abs(hash) % styles.length];
}

export default function Badge({ name }: { name: string }) {
  return <span className={`rounded-full px-2.5 py-1 text-xs font-medium ${styleFor(name)}`}>{name}</span>;
}
