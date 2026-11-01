import { useEffect, useState } from "react";
import { reembedKnowledge } from "@/features/knowledge/knowledgeApi";
import { getAiProviders, updateSetting, type AiProvidersInfo, type EmbeddingProvider } from "@/features/settings/settingsApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";

type EmbeddingServiceListProps = {
  // The current "Embedding API address" and "Embedding model" settings.
  baseUrl: string;
  model: string;
  onChanged: () => void;
};

const sameUrl = (a: string, b: string) => a.replace(/\/+$/, "") === b.replace(/\/+$/, "");

// Notes on the services that can run the embedding model (the knowledge search). "Use" sets the Embedding API
// address, the Embedding model and its two prefixes to that service's values.
export default function EmbeddingServiceList({ baseUrl, model, onChanged }: EmbeddingServiceListProps) {
  const [info, setInfo] = useState<AiProvidersInfo | null>(null);
  const [switching, setSwitching] = useState("");
  const [notice, setNotice] = useState<{ variant: "success" | "warning"; message: string } | null>(null);
  const [reembedding, setReembedding] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let ignore = false;
    getAiProviders()
      .then((loaded) => !ignore && setInfo(loaded))
      .catch(() => !ignore && setInfo(null));
    return () => {
      ignore = true;
    };
  }, []);

  if (!info) return null;

  const inUse = info.embeddingProviders.find((p) => sameUrl(p.baseUrl, baseUrl));
  const hasKey = info.hasEmbeddingApiKey || info.hasApiKey;

  // Embeds all the knowledge again, so it can be searched with the current embedding model.
  async function reembed(switchedTo?: string) {
    const before = switchedTo ? `Switched the embeddings to ${switchedTo}. ` : "";
    setReembedding(true);
    try {
      const { sections } = await reembedKnowledge();
      setNotice({ variant: "success", message: `${before}Embedded ${sections} knowledge sections again with the new model.` });
    } catch (err) {
      setNotice({
        variant: "warning",
        message: `${before}${errorMessage(err, "Embedding the knowledge again failed.")} The knowledge can't be searched until it works: fix the key or settings (restart the server after changing .env), then click Re-embed all knowledge.`
      });
    } finally {
      setReembedding(false);
    }
  }

  async function use(provider: EmbeddingProvider) {
    setError("");
    setNotice(null);
    setSwitching(provider.name);
    try {
      await updateSetting("embedding.base_url", provider.baseUrl);
      await updateSetting("embedding.model", provider.exampleModel);
      await updateSetting("embedding.query_prefix", provider.queryPrefix);
      await updateSetting("embedding.document_prefix", provider.documentPrefix);
      onChanged();
    } catch (err) {
      setError(errorMessage(err, "Could not switch the embedding model. Please try again."));
      setSwitching("");
      return;
    }
    setSwitching("");
    await reembed(provider.name);
  }

  return (
    <div className="mb-4 rounded-2xl border border-stone-200 bg-white p-6 shadow-sm">
      <p className="font-medium text-stone-900">Switching the embedding model</p>
      <div className="mt-1 space-y-1 text-sm text-stone-500">
        <p>
          The knowledge search turns text into vectors with the <span className="font-medium text-stone-700">Embedding model</span>{" "}
          at the <span className="font-medium text-stone-700">Embedding API address</span>. It can be a different service from the
          chat, e.g. Groq for the chat and Gemini for the embeddings. When hosting in the cloud without Ollama, use an online
          service here.
        </p>
        <p>
          An online service needs its key in <span className="font-mono">{info.embeddingApiKeyName}</span> in the server's{" "}
          <span className="font-mono">.env</span> file; if that is empty, <span className="font-mono">{info.apiKeyName}</span> is
          used (enough when the chat uses the same service). Restart the server after changing <span className="font-mono">.env</span>.
        </p>
        <p className="font-medium text-amber-800">
          Vectors from different models can't be compared, so all the knowledge is embedded again after switching. Clicking Use
          does this for you; after changing the embedding settings by hand, or the key, click Re-embed all knowledge.
        </p>
      </div>

      <p className="mt-3 text-sm">
        {info.hasEmbeddingApiKey ? (
          <span className="text-emerald-700">✓ {info.embeddingApiKeyName} is set</span>
        ) : info.hasApiKey ? (
          <span className="text-stone-500">
            {info.embeddingApiKeyName} is not set, so {info.apiKeyName} is used
          </span>
        ) : (
          <span className="text-stone-500">
            Neither {info.embeddingApiKeyName} nor {info.apiKeyName} is set
          </span>
        )}
      </p>
      {inUse?.needsApiKey && !hasKey && (
        <div className="mt-3">
          <Alert message={`The embeddings use ${inUse.name}, but no key is set in .env, so the knowledge search can't work.`} />
        </div>
      )}
      <div className="mt-3">
        <Button variant="secondary" size="sm" disabled={reembedding || switching !== ""} onClick={() => reembed()}>
          {reembedding ? "Embedding the knowledge..." : "Re-embed all knowledge"}
        </Button>
      </div>
      {notice && (
        <div className="mt-3">
          <Alert variant={notice.variant} message={notice.message} />
        </div>
      )}
      {error && (
        <div className="mt-3">
          <Alert message={error} />
        </div>
      )}

      <ul className="mt-4 divide-y divide-stone-100">
        {info.embeddingProviders.map((provider) => {
          const active = inUse === provider;
          return (
            <li key={provider.name} className="grid gap-2 py-4 sm:grid-cols-[1fr_auto] sm:items-start sm:gap-4">
              <div className="min-w-0 space-y-1">
                <p className="font-medium text-stone-800">
                  {provider.name}
                  {active && (
                    <span className="ml-2 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-semibold text-brand-700">In use</span>
                  )}
                </p>
                <p className="break-all text-sm text-stone-600">
                  Address: <span className="font-mono">{provider.baseUrl}</span>
                </p>
                <p className="break-all text-sm text-stone-600">
                  Model: <span className="font-mono">{provider.exampleModel}</span>
                  {active && model !== provider.exampleModel && <span className="text-stone-400"> (you use {model})</span>}
                </p>
                <p className="text-sm text-stone-600">
                  Prefixes:{" "}
                  {provider.queryPrefix || provider.documentPrefix ? (
                    <span className="font-mono">
                      {provider.queryPrefix} / {provider.documentPrefix}
                    </span>
                  ) : (
                    "none"
                  )}
                </p>
                <p className="text-sm text-stone-500">
                  {provider.freeTier}{" "}
                  <a href={provider.signUpUrl} target="_blank" rel="noreferrer" className="font-semibold text-brand-600">
                    {provider.needsApiKey ? "Get a key" : "Download"}
                  </a>
                </p>
              </div>
              <Button
                variant="secondary"
                size="sm"
                disabled={switching !== "" || reembedding || (active && model === provider.exampleModel)}
                onClick={() => use(provider)}
              >
                {switching === provider.name ? "Switching..." : "Use"}
              </Button>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
