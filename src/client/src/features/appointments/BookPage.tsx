import { Bot } from "lucide-react";
import { Link } from "react-router-dom";
import PageHeader from "@/shared/components/PageHeader";

export default function BookPage() {
  return (
    <main className="mx-auto max-w-3xl px-6 py-10">
      <PageHeader title="Book an appointment" description="Tell the assistant what is wrong and it finds a doctor and a time." />
      <Link
        to="/book/chat"
        className="mt-8 flex items-center gap-4 rounded-2xl border border-stone-200 bg-white p-6 shadow-sm transition hover:border-brand-300 hover:shadow-md"
      >
        <span className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-brand-600 text-white">
          <Bot className="size-6" aria-hidden="true" />
        </span>
        <span>
          <strong className="block text-stone-900">Don&apos;t know which doctor to book?</strong>
          <span className="text-sm text-stone-500">Ask your Vaidya Mitra</span>
        </span>
      </Link>
    </main>
  );
}
