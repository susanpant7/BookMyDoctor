import { useEffect, useRef, useState, type FormEvent } from "react";
import {
  deleteKnowledgeSource,
  DOCTORS_SOURCE,
  getChunks,
  loadDefaultKnowledge,
  searchKnowledge,
  syncDoctorProfiles,
  uploadKnowledge,
  type KnowledgeChunk,
  type KnowledgeSearchResult,
  type UploadFileResult
} from "@/features/knowledge/knowledgeApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Badge from "@/shared/components/Badge";
import Button from "@/shared/components/Button";
import ConfirmDialog from "@/shared/components/ConfirmDialog";
import PageHeader from "@/shared/components/PageHeader";
import Tabs from "@/shared/components/Tabs";

const ALL = "All";

export default function KnowledgePage() {
  const [chunks, setChunks] = useState<KnowledgeChunk[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [results, setResults] = useState<UploadFileResult[]>([]);
  const [activeTab, setActiveTab] = useState(ALL);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [files, setFiles] = useState<File[]>([]);
  const [busy, setBusy] = useState(false);
  const [deleting, setDeleting] = useState<string | null>(null);
  const [version, setVersion] = useState(0);
  const fileInput = useRef<HTMLInputElement>(null);
  const reload = () => setVersion((v) => v + 1);

  useEffect(() => {
    let ignore = false;
    getChunks()
      .then((loaded) => !ignore && setChunks(loaded))
      .catch((err) => !ignore && setError(errorMessage(err, "Could not load the knowledge data.")))
      .finally(() => !ignore && setLoading(false));
    return () => {
      ignore = true;
    };
  }, [version]);

  // Runs one action with a busy flag and shows what it reports.
  async function run(action: () => Promise<UploadFileResult[] | void>, success: string) {
    setError("");
    setNotice("");
    setResults([]);
    setBusy(true);
    try {
      const uploaded = await action();
      if (uploaded) setResults(uploaded);
      else setNotice(success);
      reload();
    } catch (err) {
      setError(errorMessage(err, "That did not work. Please try again."));
    } finally {
      setBusy(false);
    }
  }

  function handleUpload(e: FormEvent) {
    e.preventDefault();
    void run(async () => {
      const uploaded = await uploadKnowledge(files);
      setFiles([]);
      if (fileInput.current) fileInput.current.value = "";
      return uploaded;
    }, "");
  }

  async function confirmDelete() {
    if (!deleting) return;
    setBusy(true);
    try {
      const { deleted } = await deleteKnowledgeSource(deleting);
      setNotice(`Deleted ${deleted} ${deleted === 1 ? "section" : "sections"} of ${deleting}.`);
      reload();
    } catch (err) {
      setError(errorMessage(err, "Could not delete the file's sections."));
    } finally {
      setBusy(false);
      setDeleting(null);
    }
  }

  // One tab per source found in the data, so a new file gets its own tab automatically.
  const sources = [...new Set(chunks.map((c) => c.source))].sort();
  const selectedTab = sources.includes(activeTab) ? activeTab : ALL;
  const visible = selectedTab === ALL ? chunks : chunks.filter((c) => c.source === selectedTab);
  const tabs = [
    { id: ALL, label: ALL, count: chunks.length },
    ...sources.map((source) => ({ id: source, label: source, count: chunks.filter((c) => c.source === source).length }))
  ];

  return (
    <main className="mx-auto max-w-6xl px-6 py-10">
      <PageHeader
        title="Knowledge"
        description="The assistant answers hospital questions from this knowledge base: the Markdown files uploaded here, and a profile for every doctor."
        action={
          <div className="flex gap-3">
            <Button variant="secondary" disabled={busy} onClick={() => run(syncDoctorProfiles, "Doctor profiles are up to date.")}>
              Sync doctor profiles
            </Button>
            <Button variant="secondary" disabled={busy} onClick={() => run(loadDefaultKnowledge, "")}>
              Load default files
            </Button>
          </div>
        }
      />

      <form onSubmit={handleUpload} className="mt-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h2 className="font-semibold text-slate-900">Upload knowledge files</h2>
        <p className="mt-1 text-sm text-slate-500">
          Markdown (.md) files. Each section starts with a <code className="rounded bg-slate-100 px-1">## Title</code> line.
          Uploading a file with the same name replaces its sections.
        </p>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <input
            ref={fileInput}
            type="file"
            accept=".md"
            multiple
            onChange={(e) => setFiles([...(e.target.files ?? [])])}
            className="block text-sm text-slate-600 file:mr-3 file:rounded-lg file:border-0 file:bg-slate-100 file:px-3 file:py-2 file:text-sm file:font-medium file:text-slate-700 hover:file:bg-slate-200"
          />
          <Button type="submit" disabled={busy || files.length === 0}>
            {busy ? "Working..." : "Upload"}
          </Button>
        </div>
      </form>

      <div className="mt-6 space-y-3">
        {notice && <Alert variant="success" message={notice} />}
        {error && <Alert message={error} />}
        {results.map((result) => (
          <Alert
            key={result.fileName}
            variant={result.error ? "error" : "success"}
            message={result.error ? `${result.fileName}: ${result.error}` : `${result.fileName}: ${result.sections} sections stored.`}
          />
        ))}
      </div>

      <SearchTester />

      <div className="mt-6 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        {loading ? (
          <p className="px-6 py-12 text-center text-sm text-slate-500">Loading...</p>
        ) : chunks.length === 0 ? (
          <p className="px-6 py-12 text-center text-sm text-slate-500">
            The knowledge base is empty. Upload files above, or load the default files.
          </p>
        ) : (
          <>
            <Tabs tabs={tabs} active={selectedTab} onChange={setActiveTab} />
            {selectedTab !== ALL && selectedTab !== DOCTORS_SOURCE && (
              <div className="flex justify-end border-b border-slate-100 px-6 py-2">
                <Button variant="dangerGhost" size="sm" onClick={() => setDeleting(selectedTab)}>
                  Delete {selectedTab}
                </Button>
              </div>
            )}
            <table className="w-full text-left text-sm">
              <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-6 py-3 font-medium">Section</th>
                  {selectedTab === ALL && <th className="px-6 py-3 font-medium">File</th>}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {visible.map((chunk) => (
                  <tr
                    key={chunk.id}
                    onClick={() => setExpandedId(expandedId === chunk.id ? null : chunk.id)}
                    className="cursor-pointer align-top hover:bg-slate-50/60"
                  >
                    <td className="px-6 py-4">
                      <p className="font-medium text-slate-900">{chunk.title}</p>
                      <p className={`mt-1 max-w-2xl whitespace-pre-wrap text-slate-500 ${expandedId === chunk.id ? "" : "line-clamp-1"}`}>
                        {chunk.text.replace(/^## .*\n/, "")}
                      </p>
                    </td>
                    {selectedTab === ALL && (
                      <td className="px-6 py-4">
                        <Badge name={chunk.source} />
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}
      </div>

      {deleting && (
        <ConfirmDialog
          title="Delete knowledge file"
          message={`Delete every section of ${deleting} from the knowledge base? The assistant will no longer answer from it. Upload the file again to bring it back.`}
          confirmLabel="Delete"
          busy={busy}
          onConfirm={confirmDelete}
          onCancel={() => setDeleting(null)}
        />
      )}
    </main>
  );
}

// Shows which sections a question would retrieve, and how well each matches (0 to 1).
function SearchTester() {
  const [query, setQuery] = useState("");
  const [found, setFound] = useState<KnowledgeSearchResult[] | null>(null);
  const [error, setError] = useState("");
  const [searching, setSearching] = useState(false);

  async function handleSearch(e: FormEvent) {
    e.preventDefault();
    setError("");
    setSearching(true);
    try {
      setFound(await searchKnowledge(query.trim()));
    } catch (err) {
      setFound(null);
      setError(errorMessage(err, "The search failed. Please try again."));
    } finally {
      setSearching(false);
    }
  }

  return (
    <section className="mt-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <h2 className="font-semibold text-slate-900">Test a question</h2>
      <p className="mt-1 text-sm text-slate-500">
        See which sections the assistant would find for a question. Use the scores to tune the minimum similarity on the Settings page.
      </p>
      <form onSubmit={handleSearch} className="mt-4 flex gap-3">
        <input
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Can I get a refund if I cancel?"
          maxLength={500}
          aria-label="Question"
          className="block w-full rounded-lg border-0 bg-white px-3 py-2.5 text-sm text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-inset focus:ring-indigo-600"
        />
        <Button type="submit" disabled={searching || query.trim().length === 0}>
          {searching ? "Searching..." : "Search"}
        </Button>
      </form>
      {error && (
        <div className="mt-4">
          <Alert message={error} />
        </div>
      )}
      {found && (
        <ul className="mt-4 divide-y divide-slate-100 rounded-lg ring-1 ring-inset ring-slate-200">
          {found.length === 0 && <li className="px-4 py-3 text-sm text-slate-500">Nothing found.</li>}
          {found.map((result) => (
            <li key={result.id} className="flex items-start gap-4 px-4 py-3">
              <span className="w-12 shrink-0 font-mono text-sm font-semibold text-indigo-700">{result.score.toFixed(3)}</span>
              <div className="min-w-0">
                <p className="text-sm font-medium text-slate-900">
                  {result.title} <span className="font-normal text-slate-500">· {result.source}</span>
                </p>
                <p className="mt-0.5 line-clamp-2 text-sm text-slate-500">{result.text.replace(/^## .*\n/, "")}</p>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
