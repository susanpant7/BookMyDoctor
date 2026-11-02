import { useEffect, useRef, useState, type FormEvent } from "react";
import {
  deleteKnowledgeSource,
  GENERATED_SOURCES,
  getChunks,
  saveKnowledgeDocument,
  searchKnowledge,
  sectionsOf,
  syncSettings,
  uploadKnowledge,
  type KnowledgeChunk,
  type KnowledgeSearchResult,
  type KnowledgeSection,
  type UploadFileResult
} from "@/features/knowledge/knowledgeApi";
import KnowledgeEditor from "@/features/knowledge/KnowledgeEditor";
import SectionForm from "@/features/knowledge/SectionForm";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Badge from "@/shared/components/Badge";
import Button from "@/shared/components/Button";
import ConfirmDialog from "@/shared/components/ConfirmDialog";
import PageHeader from "@/shared/components/PageHeader";
import Tabs from "@/shared/components/Tabs";

const ALL = "All";

// One section of a file, by its place in the file. index null = a new section at the end.
type SectionRef = { source: string; index: number | null };

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
  // The file being written: null = closed, "" = a new file, otherwise the name of the file being edited.
  const [editing, setEditing] = useState<string | null>(null);
  const [editingSection, setEditingSection] = useState<SectionRef | null>(null);
  const [deletingSection, setDeletingSection] = useState<SectionRef | null>(null);
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

  // Shown in place of the file name. Files written or uploaded with a "# " title show that title.
  const titleOf = (source: string) => chunks.find((c) => c.source === source)?.documentTitle ?? source;
  const editable = (source: string) => !GENERATED_SOURCES.includes(source);

  // A section is changed by saving its whole file again with the new list of sections.
  async function saveSections(source: string, sections: KnowledgeSection[]) {
    const result = await saveKnowledgeDocument({ fileName: source, title: titleOf(source), sections });
    if (result.error) throw new Error(result.error);
    setError("");
    setResults([]);
    reload();
  }

  async function saveSection(section: KnowledgeSection) {
    if (!editingSection) return;
    const { source, index } = editingSection;
    const sections = sectionsOf(chunks, source);
    await saveSections(source, index === null ? [...sections, section] : sections.map((s, i) => (i === index ? section : s)));
    setNotice(index === null ? `Added "${section.title}" to ${titleOf(source)}.` : `Saved "${section.title}".`);
    setEditingSection(null);
  }

  async function confirmDeleteSection() {
    if (!deletingSection || deletingSection.index === null) return;
    const { source, index } = deletingSection;
    const sections = sectionsOf(chunks, source);
    setBusy(true);
    setNotice("");
    try {
      // The last section goes with its file, since a file needs at least one section.
      if (sections.length === 1) await deleteKnowledgeSource(source);
      else await saveSections(source, sections.filter((_, i) => i !== index));
      setNotice(`Deleted "${sections[index].title}" from ${titleOf(source)}.`);
      reload();
    } catch (err) {
      setError(err instanceof Error && !(err instanceof TypeError) ? err.message : errorMessage(err, "Could not delete the section."));
    } finally {
      setBusy(false);
      setDeletingSection(null);
    }
  }

  async function confirmDelete() {
    if (!deleting) return;
    setBusy(true);
    try {
      const { deleted } = await deleteKnowledgeSource(deleting);
      setNotice(`Deleted ${deleted} ${deleted === 1 ? "section" : "sections"} of ${titleOf(deleting)}.`);
      reload();
    } catch (err) {
      setError(errorMessage(err, "Could not delete the file's sections."));
    } finally {
      setBusy(false);
      setDeleting(null);
    }
  }

  // One tab per source found in the data, so a new file gets its own tab automatically.
  const sources = [...new Set(chunks.map((c) => c.source))].sort((a, b) => titleOf(a).localeCompare(titleOf(b)));
  const selectedTab = sources.includes(activeTab) ? activeTab : ALL;
  const visible = selectedTab === ALL ? chunks : chunks.filter((c) => c.source === selectedTab);
  const tabs = [
    { id: ALL, label: ALL, count: chunks.length },
    ...sources.map((source) => ({ id: source, label: titleOf(source), count: chunks.filter((c) => c.source === source).length }))
  ];

  return (
    <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6 sm:py-10">
      <PageHeader
        title="Knowledge"
        description="The assistant answers hospital questions from this knowledge base: the Markdown files uploaded here, plus doctor-settings.md, specialty-settings.md and hospital-settings.md, which the server builds from the doctors, specialties and hospital settings."
        action={
          <Button variant="secondary" disabled={busy} onClick={() => run(syncSettings, "doctor-settings.md, specialty-settings.md and hospital-settings.md are up to date.")}>
            Sync settings
          </Button>
        }
      />

      <form onSubmit={handleUpload} className="mt-6 rounded-2xl border border-stone-200 bg-white p-4 shadow-sm sm:p-6">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <h2 className="font-semibold text-stone-900">Upload knowledge files</h2>
          <Button variant="secondary" size="sm" disabled={busy} onClick={() => setEditing("")}>
            Write knowledge
          </Button>
        </div>
        <p className="mt-1 text-sm text-stone-500">
          Markdown (.md) files: a <code className="rounded bg-stone-100 px-1"># Title</code> line, then each section starting
          with a <code className="rounded bg-stone-100 px-1">## Section name</code> line. Uploading a file with the same name
          replaces its sections. Or type it in with Write knowledge, no Markdown needed.
        </p>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <input
            ref={fileInput}
            type="file"
            accept=".md"
            multiple
            onChange={(e) => setFiles([...(e.target.files ?? [])])}
            className="block text-sm text-stone-600 file:mr-3 file:rounded-lg file:border-0 file:bg-stone-100 file:px-3 file:py-2 file:text-sm file:font-medium file:text-stone-700 hover:file:bg-stone-200"
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
            message={result.error ? `${titleOf(result.fileName)}: ${result.error}` : `${titleOf(result.fileName)}: ${result.sections} sections stored.`}
          />
        ))}
      </div>

      <SearchTester />

      <div className="mt-6 overflow-hidden rounded-2xl border border-stone-200 bg-white shadow-sm">
        {loading ? (
          <p className="px-6 py-12 text-center text-sm text-stone-500">Loading...</p>
        ) : chunks.length === 0 ? (
          <p className="px-6 py-12 text-center text-sm text-stone-500">
            The knowledge base is empty. Upload or write files above, or load the default files in Settings → Manage App Data.
          </p>
        ) : (
          <>
            <Tabs tabs={tabs} active={selectedTab} onChange={setActiveTab} />
            {selectedTab !== ALL && editable(selectedTab) && (
              <div className="flex flex-wrap justify-end gap-1 border-b border-stone-100 px-6 py-2">
                <Button variant="ghost" size="sm" disabled={busy} onClick={() => setEditingSection({ source: selectedTab, index: null })}>
                  Add section
                </Button>
                <Button variant="ghost" size="sm" disabled={busy} onClick={() => setEditing(selectedTab)}>
                  Edit all
                </Button>
                <Button variant="dangerGhost" size="sm" disabled={busy} onClick={() => setDeleting(selectedTab)}>
                  Delete {titleOf(selectedTab)}
                </Button>
              </div>
            )}
            <table className="w-full text-left text-sm">
              <thead className="bg-stone-50 text-xs uppercase tracking-wide text-stone-500">
                <tr>
                  <th className="px-4 py-3 sm:px-6 font-medium">Section</th>
                  {selectedTab === ALL && <th className="px-4 py-3 sm:px-6 font-medium">Knowledge</th>}
                  <th className="px-4 py-3 sm:px-6" />
                </tr>
              </thead>
              <tbody className="divide-y divide-stone-100">
                {visible.map((chunk) => {
                  const index = chunks.filter((c) => c.source === chunk.source).indexOf(chunk);
                  return (
                  <tr
                    key={chunk.id}
                    onClick={() => setExpandedId(expandedId === chunk.id ? null : chunk.id)}
                    className="cursor-pointer align-top hover:bg-stone-50/60"
                  >
                    <td className="px-4 py-4 sm:px-6">
                      <p className="font-medium text-stone-900">{chunk.title}</p>
                      <p className={`mt-1 max-w-2xl whitespace-pre-wrap text-stone-500 ${expandedId === chunk.id ? "" : "line-clamp-1"}`}>
                        {chunk.text.replace(/^## .*\n/, "")}
                      </p>
                    </td>
                    {selectedTab === ALL && (
                      <td className="px-4 py-4 sm:px-6">
                        <Badge name={chunk.documentTitle} />
                      </td>
                    )}
                    <td className="px-4 py-3 sm:px-6">
                      {editable(chunk.source) && (
                        <div className="flex justify-end gap-1" onClick={(e) => e.stopPropagation()}>
                          <Button variant="ghost" size="sm" disabled={busy} onClick={() => setEditingSection({ source: chunk.source, index })}>
                            Edit
                          </Button>
                          <Button variant="dangerGhost" size="sm" disabled={busy} onClick={() => setDeletingSection({ source: chunk.source, index })}>
                            Delete
                          </Button>
                        </div>
                      )}
                    </td>
                  </tr>
                  );
                })}
              </tbody>
            </table>
          </>
        )}
      </div>

      {editing !== null && (
        <KnowledgeEditor
          key={editing}
          fileName={editing || null}
          initialTitle={editing ? titleOf(editing) : ""}
          initialSections={editing ? sectionsOf(chunks, editing) : []}
          onSaved={(result) => {
            setEditing(null);
            setError("");
            setNotice("");
            setResults([result]);
            setActiveTab(result.fileName);
            reload();
          }}
          onCancel={() => setEditing(null)}
        />
      )}

      {editingSection && (
        <SectionForm
          documentTitle={titleOf(editingSection.source)}
          section={editingSection.index === null ? null : sectionsOf(chunks, editingSection.source)[editingSection.index]}
          onSave={saveSection}
          onCancel={() => setEditingSection(null)}
        />
      )}

      {deletingSection && deletingSection.index !== null && (
        <ConfirmDialog
          title="Delete section"
          message={
            sectionsOf(chunks, deletingSection.source).length === 1
              ? `"${sectionsOf(chunks, deletingSection.source)[0].title}" is the only section of ${titleOf(deletingSection.source)}, so the whole knowledge file is deleted. This cannot be undone.`
              : `Delete "${sectionsOf(chunks, deletingSection.source)[deletingSection.index].title}" from ${titleOf(deletingSection.source)}? The assistant will no longer answer from it. This cannot be undone.`
          }
          confirmLabel="Delete"
          busy={busy}
          onConfirm={confirmDeleteSection}
          onCancel={() => setDeletingSection(null)}
        />
      )}

      {deleting && (
        <ConfirmDialog
          title="Delete knowledge file"
          message={`Delete every section of ${titleOf(deleting)} from the knowledge base? The assistant will no longer answer from it. This cannot be undone.`}
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
    <section className="mt-6 rounded-2xl border border-stone-200 bg-white p-4 shadow-sm sm:p-6">
      <h2 className="font-semibold text-stone-900">Test a question</h2>
      <p className="mt-1 text-sm text-stone-500">
        See which sections the assistant would find for a question. Use the scores to tune the minimum similarity on the Settings page.
      </p>
      <form onSubmit={handleSearch} className="mt-4 flex gap-3">
        <input
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Can I get a refund if I cancel?"
          maxLength={500}
          aria-label="Question"
          className="block w-full rounded-lg border-0 bg-white px-3 py-2.5 text-sm text-stone-900 shadow-sm ring-1 ring-inset ring-stone-300 placeholder:text-stone-400 focus:outline-none focus:ring-2 focus:ring-inset focus:ring-brand-600"
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
        <ul className="mt-4 divide-y divide-stone-100 rounded-lg ring-1 ring-inset ring-stone-200">
          {found.length === 0 && <li className="px-4 py-3 text-sm text-stone-500">Nothing found.</li>}
          {found.map((result) => (
            <li key={result.id} className="flex items-start gap-4 px-4 py-3">
              <span className="w-12 shrink-0 font-mono text-sm font-semibold text-brand-700">{result.score.toFixed(3)}</span>
              <div className="min-w-0">
                <p className="text-sm font-medium text-stone-900">
                  {result.title} <span className="font-normal text-stone-500">· {result.source}</span>
                </p>
                <p className="mt-0.5 line-clamp-2 text-sm text-stone-500">{result.text.replace(/^## .*\n/, "")}</p>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
