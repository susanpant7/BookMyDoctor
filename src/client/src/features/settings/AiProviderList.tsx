import { useEffect, useState } from "react";
import { getAiProviders, type AiProviderInfo } from "@/features/settings/settingsApi";

type AiProviderListProps = {
  // The value of the "AI provider" setting.
  current: string;
};

// Every provider the "AI provider" setting can choose, and whether its key is in the server's .env file.
export default function AiProviderList({ current }: AiProviderListProps) {
  const [providers, setProviders] = useState<AiProviderInfo[]>([]);

  useEffect(() => {
    let ignore = false;
    getAiProviders()
      .then((loaded) => !ignore && setProviders(loaded))
      .catch(() => !ignore && setProviders([]));
    return () => {
      ignore = true;
    };
  }, []);

  if (providers.length === 0) return null;

  const chosen = providers.find((p) => p.id === current);

  return (
    <div className="mb-4 rounded-2xl border border-stone-200 bg-white p-6 shadow-sm">
      <p className="font-medium text-stone-900">AI providers</p>
      <p className="mt-1 text-sm text-stone-500">
        Pick one with the AI provider setting below; its model is under Model for each provider. Online providers need their
        API key in the server's <span className="font-mono">.env</span> file (see <span className="font-mono">.env.example</span>),
        then a server restart. Keys are never stored in the database or shown here.
      </p>
      {chosen && !chosen.hasApiKey && (
        <p className="mt-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700 ring-1 ring-inset ring-red-200">
          The assistant uses {chosen.name}, but <span className="font-mono">{chosen.apiKeyName}</span> is not set, so it can't reply.
        </p>
      )}
      <ul className="mt-4 divide-y divide-stone-100">
        {providers.map((provider) => (
          <li key={provider.id} className="grid gap-1 py-3 sm:grid-cols-[13rem_1fr_auto] sm:items-baseline sm:gap-4">
            <p className="font-medium text-stone-800">
              {provider.name}
              {provider.id === current && (
                <span className="ml-2 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-semibold text-brand-700">In use</span>
              )}
            </p>
            <p className="text-sm text-stone-500">
              <span className="font-mono text-stone-700">{provider.id}</span> · {provider.freeTier}{" "}
              <a href={provider.signUpUrl} target="_blank" rel="noreferrer" className="font-semibold text-brand-600">
                {provider.apiKeyName ? "Get a key" : "Download"}
              </a>
            </p>
            <p className="text-sm">
              {provider.apiKeyName === null ? (
                <span className="text-stone-500">No key needed</span>
              ) : provider.hasApiKey ? (
                <span className="text-emerald-700">✓ {provider.apiKeyName} set</span>
              ) : (
                <span className="text-stone-400">{provider.apiKeyName} not set</span>
              )}
            </p>
          </li>
        ))}
      </ul>
    </div>
  );
}
