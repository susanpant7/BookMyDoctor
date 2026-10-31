import { useEffect, useState, type FormEvent } from "react";
import { dayNames, shortTime, weekOrder } from "@/features/doctors/doctorsApi";
import { getHospital, saveHospital, type HospitalDetails } from "@/features/settings/settingsApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import { TextAreaField, TextField } from "@/shared/components/Field";

// What patients and the assistant are told about the hospital itself: contact details, opening hours, prices and insurance.
export default function HospitalSettings() {
  const [details, setDetails] = useState<HospitalDetails | null>(null);
  const [facilities, setFacilities] = useState("");
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [saving, setSaving] = useState(false);

  function show(loaded: HospitalDetails) {
    setDetails({
      ...loaded,
      hours: loaded.hours.map((h) => ({ ...h, opens: shortTime(h.opens), closes: shortTime(h.closes) }))
    });
    setFacilities(loaded.info.facilities.join("\n"));
  }

  useEffect(() => {
    getHospital()
      .then(show)
      .catch((err) => setError(errorMessage(err, "Could not load the hospital details.")));
  }, []);

  if (!details) {
    return error ? <Alert message={error} /> : <p className="mt-8 text-center text-sm text-stone-500">Loading...</p>;
  }

  const { info, hours, servicePrices, insurancePlans } = details;
  const change = (changes: Partial<HospitalDetails>) => setDetails({ ...details, ...changes });
  const changeInfo = (changes: Partial<typeof info>) => change({ info: { ...info, ...changes } });
  const hoursFor = (day: number) => hours.find((h) => h.day === day);

  function setOpen(day: number, open: boolean) {
    change({
      hours: open
        ? [...hours, { day, opens: "08:00", closes: "19:00" }]
        : hours.filter((h) => h.day !== day)
    });
  }

  function changeHours(day: number, changes: { opens?: string; closes?: string }) {
    change({ hours: hours.map((h) => (h.day === day ? { ...h, ...changes } : h)) });
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError("");
    setNotice("");
    setSaving(true);
    try {
      show(
        await saveHospital({
          ...details!,
          info: { ...info, facilities: facilities.split("\n").map((f) => f.trim()).filter(Boolean) }
        })
      );
      setNotice("Saved. The next patient question uses the new details.");
    } catch (err) {
      setError(errorMessage(err, "Could not save the hospital details. Please try again."));
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      <Section title="Contact details">
        <div className="grid gap-5 sm:grid-cols-2">
          <TextField label="Hospital name" required maxLength={100} value={info.name} onChange={(e) => changeInfo({ name: e.target.value })} />
          <TextField label="Phone" value={info.phone} onChange={(e) => changeInfo({ phone: e.target.value })} />
        </div>
        <TextField label="Address" value={info.address} onChange={(e) => changeInfo({ address: e.target.value })} />
        <div className="grid gap-5 sm:grid-cols-2">
          <TextField label="Parking" value={info.parking} onChange={(e) => changeInfo({ parking: e.target.value })} />
          <TextField label="Public transport" value={info.publicTransport} onChange={(e) => changeInfo({ publicTransport: e.target.value })} />
        </div>
        <TextAreaField label="Facilities (one per line)" rows={4} value={facilities} onChange={(e) => setFacilities(e.target.value)} />
      </Section>

      <Section title="Opening hours">
        <ul className="space-y-2">
          {weekOrder.map((day) => {
            const open = hoursFor(day);
            return (
              <li key={day} className="grid grid-cols-[8rem_1fr] items-center gap-3 sm:grid-cols-[8rem_9rem_9rem_1fr]">
                <label className="flex items-center gap-2 text-sm font-medium text-stone-700">
                  <input type="checkbox" checked={open !== undefined} onChange={(e) => setOpen(day, e.target.checked)} />
                  {dayNames[day]}
                </label>
                {open ? (
                  <>
                    <input
                      type="time"
                      required
                      aria-label={`${dayNames[day]} opens`}
                      value={open.opens}
                      onChange={(e) => changeHours(day, { opens: e.target.value })}
                      className="rounded-lg border-0 px-3 py-2 text-sm shadow-sm ring-1 ring-inset ring-stone-300"
                    />
                    <input
                      type="time"
                      required
                      aria-label={`${dayNames[day]} closes`}
                      value={open.closes}
                      onChange={(e) => changeHours(day, { closes: e.target.value })}
                      className="rounded-lg border-0 px-3 py-2 text-sm shadow-sm ring-1 ring-inset ring-stone-300"
                    />
                  </>
                ) : (
                  <span className="text-sm text-stone-500">Closed</span>
                )}
              </li>
            );
          })}
        </ul>
      </Section>

      <Section title="Extra test prices">
        <ul className="space-y-2">
          {servicePrices.map((price, index) => (
            <li key={index} className="grid grid-cols-[1fr_7rem_auto] items-center gap-3">
              <input
                required
                aria-label="Test name"
                value={price.name}
                onChange={(e) => change({ servicePrices: servicePrices.map((p, i) => (i === index ? { ...p, name: e.target.value } : p)) })}
                className="rounded-lg border-0 px-3 py-2 text-sm shadow-sm ring-1 ring-inset ring-stone-300"
              />
              <input
                required
                type="number"
                min={0}
                step="0.01"
                aria-label="Price"
                value={price.price}
                onChange={(e) => change({ servicePrices: servicePrices.map((p, i) => (i === index ? { ...p, price: Number(e.target.value) } : p)) })}
                className="rounded-lg border-0 px-3 py-2 text-sm shadow-sm ring-1 ring-inset ring-stone-300"
              />
              <Button variant="dangerGhost" size="sm" onClick={() => change({ servicePrices: servicePrices.filter((_, i) => i !== index) })}>
                Remove
              </Button>
            </li>
          ))}
        </ul>
        <Button variant="secondary" size="sm" onClick={() => change({ servicePrices: [...servicePrices, { id: "", name: "", price: 0 }] })}>
          Add test
        </Button>
      </Section>

      <Section title="Insurance plans accepted">
        <ul className="space-y-2">
          {insurancePlans.map((plan, index) => (
            <li key={index} className="grid grid-cols-[1fr_1fr_auto] items-center gap-3">
              <input
                required
                aria-label="Plan name"
                value={plan.name}
                onChange={(e) => change({ insurancePlans: insurancePlans.map((p, i) => (i === index ? { ...p, name: e.target.value } : p)) })}
                className="rounded-lg border-0 px-3 py-2 text-sm shadow-sm ring-1 ring-inset ring-stone-300"
              />
              <input
                placeholder="Restrictions (optional)"
                aria-label="Restrictions"
                value={plan.restrictions ?? ""}
                onChange={(e) =>
                  change({ insurancePlans: insurancePlans.map((p, i) => (i === index ? { ...p, restrictions: e.target.value || null } : p)) })
                }
                className="rounded-lg border-0 px-3 py-2 text-sm shadow-sm ring-1 ring-inset ring-stone-300"
              />
              <Button variant="dangerGhost" size="sm" onClick={() => change({ insurancePlans: insurancePlans.filter((_, i) => i !== index) })}>
                Remove
              </Button>
            </li>
          ))}
        </ul>
        <Button variant="secondary" size="sm" onClick={() => change({ insurancePlans: [...insurancePlans, { id: "", name: "", restrictions: null }] })}>
          Add plan
        </Button>
      </Section>

      {notice && <Alert variant="success" message={notice} />}
      {error && <Alert message={error} />}

      <div className="flex justify-end">
        <Button type="submit" disabled={saving}>
          {saving ? "Saving..." : "Save hospital details"}
        </Button>
      </div>
    </form>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="space-y-4 rounded-2xl border border-stone-200 bg-white p-6 shadow-sm">
      <h2 className="font-semibold text-stone-900">{title}</h2>
      {children}
    </section>
  );
}
