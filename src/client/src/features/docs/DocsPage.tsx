import { useSearchParams } from "react-router-dom";
import installation from "@/features/docs/how-to-run-and-use.md?raw";
import technicalGuide from "@/features/docs/technical-guide.md?raw";
import MarkdownDocument from "@/shared/components/MarkdownDocument";
import PageHeader from "@/shared/components/PageHeader";
import Tabs from "@/shared/components/Tabs";

const tabs = [
  { id: "installation", label: "Installation" },
  { id: "ai-rag", label: "AI and RAG" }
];

/** The developer docs, open to everyone: how to run, set up and use the app, and how its AI and RAG work. */
export default function DocsPage() {
  // The tab is in the address (?tab=ai-rag), so a link can open either one.
  const [params, setParams] = useSearchParams();
  const tab = params.get("tab") === "ai-rag" ? "ai-rag" : "installation";

  return (
    <main className="mx-auto max-w-6xl px-6 py-10">
      <PageHeader title="Dev Docs" description="How to run, set up and use the app, and how its AI and RAG work." />
      <div className="mt-6 overflow-hidden rounded-2xl border border-stone-200 bg-white shadow-sm">
        <Tabs tabs={tabs} active={tab} onChange={(id) => setParams(id === "installation" ? {} : { tab: id })} />
      </div>
      <div className="mt-6">
        <MarkdownDocument key={tab} markdown={tab === "installation" ? installation : technicalGuide} />
      </div>
    </main>
  );
}
