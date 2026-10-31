import { useEffect, useState, type FormEvent } from "react";
import {
  createDoctor,
  dayNames,
  getSchedule,
  shortTime,
  updateDoctor,
  weekOrder,
  type Doctor,
  type ScheduleBlock
} from "@/features/doctors/doctorsApi";
import type { Specialty } from "@/features/specialties/specialtiesApi";
import { errorMessage } from "@/shared/api/errorMessage";
import Alert from "@/shared/components/Alert";
import Button from "@/shared/components/Button";
import { SelectField, TextAreaField, TextField } from "@/shared/components/Field";
import Modal from "@/shared/components/Modal";

type DoctorFormProps = {
  doctor: Doctor | null;
  specialties: Specialty[];
  onSaved: () => void;
  onCancel: () => void;
};

const newBlock: ScheduleBlock = { day: 1, start: "09:00", end: "17:00", slotMinutes: 30 };

export default function DoctorForm({ doctor, specialties, onSaved, onCancel }: DoctorFormProps) {
  const [name, setName] = useState(doctor?.name ?? "");
  const [specialtyId, setSpecialtyId] = useState(doctor?.specialtyId ?? specialties[0]?.id ?? "");
  const [experienceYears, setExperienceYears] = useState(String(doctor?.experienceYears ?? 0));
  const [consultationFee, setConsultationFee] = useState(String(doctor?.consultationFee ?? 0));
  const [followUpFee, setFollowUpFee] = useState(String(doctor?.followUpFee ?? 0));
  const [languages, setLanguages] = useState(doctor?.languages.join(", ") ?? "English");
  const [bio, setBio] = useState(doctor?.bio ?? "");
  const [schedule, setSchedule] = useState<ScheduleBlock[]>([]);
  const [scheduleLoaded, setScheduleLoaded] = useState(doctor === null);
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!doctor) return;
    let ignore = false;
    getSchedule(doctor.id)
      .then((blocks) => {
        if (ignore) return;
        setSchedule(blocks.map((block) => ({ ...block, start: shortTime(block.start), end: shortTime(block.end) })));
        setScheduleLoaded(true);
      })
      .catch((err) => !ignore && setError(errorMessage(err, "Could not load the weekly schedule.")));
    return () => {
      ignore = true;
    };
  }, [doctor]);

  function changeBlock(index: number, changes: Partial<ScheduleBlock>) {
    setSchedule((blocks) => blocks.map((block, i) => (i === index ? { ...block, ...changes } : block)));
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError("");
    setSubmitting(true);
    try {
      const input = {
        name,
        specialtyId,
        experienceYears: Number(experienceYears),
        consultationFee: Number(consultationFee),
        followUpFee: Number(followUpFee),
        languages: languages.split(",").map((language) => language.trim()).filter(Boolean),
        bio,
        schedule
      };
      if (doctor) await updateDoctor(doctor.id, input);
      else await createDoctor(input);
      onSaved();
    } catch (err) {
      setError(errorMessage(err, "Something went wrong. Please try again."));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal title={doctor ? "Edit doctor" : "Add doctor"} onClose={onCancel} maxWidth="max-w-2xl">
      <form onSubmit={handleSubmit} className="space-y-5">
        <div className="grid gap-5 sm:grid-cols-2">
          <TextField label="Name" required maxLength={100} value={name} onChange={(e) => setName(e.target.value)} />
          <SelectField label="Specialty" required value={specialtyId} onChange={(e) => setSpecialtyId(e.target.value)}>
            {specialties.map((specialty) => (
              <option key={specialty.id} value={specialty.id}>
                {specialty.name}
              </option>
            ))}
          </SelectField>
        </div>

        <div className="grid gap-5 sm:grid-cols-3">
          <TextField
            label="Years of experience"
            type="number"
            min={0}
            max={70}
            required
            value={experienceYears}
            onChange={(e) => setExperienceYears(e.target.value)}
          />
          <TextField
            label="New visit fee ($)"
            type="number"
            min={0}
            step="0.01"
            required
            value={consultationFee}
            onChange={(e) => setConsultationFee(e.target.value)}
          />
          <TextField
            label="Follow-up fee ($)"
            type="number"
            min={0}
            step="0.01"
            required
            value={followUpFee}
            onChange={(e) => setFollowUpFee(e.target.value)}
          />
        </div>

        <TextField
          label="Languages"
          placeholder="English, Hindi"
          value={languages}
          onChange={(e) => setLanguages(e.target.value)}
        />
        <TextAreaField
          label="Bio"
          rows={3}
          maxLength={1000}
          placeholder="What the doctor focuses on. The assistant uses this to recommend the right doctor."
          value={bio}
          onChange={(e) => setBio(e.target.value)}
        />

        <fieldset>
          <div className="flex items-center justify-between">
            <legend className="text-sm font-medium text-stone-700">Weekly schedule (hospital time)</legend>
            <Button variant="ghost" size="sm" onClick={() => setSchedule((blocks) => [...blocks, newBlock])}>
              Add hours
            </Button>
          </div>
          {!scheduleLoaded ? (
            <p className="mt-2 text-sm text-stone-500">Loading...</p>
          ) : schedule.length === 0 ? (
            <p className="mt-2 text-sm text-stone-500">No hours yet, so patients cannot book this doctor.</p>
          ) : (
            <ul className="mt-2 space-y-2">
              {schedule.map((block, index) => (
                <li key={index} className="grid grid-cols-2 items-end gap-2 sm:grid-cols-[1fr_1fr_1fr_5rem_auto]">
                  <SelectField label="Day" value={block.day} onChange={(e) => changeBlock(index, { day: Number(e.target.value) })}>
                    {weekOrder.map((day) => (
                      <option key={day} value={day}>
                        {dayNames[day]}
                      </option>
                    ))}
                  </SelectField>
                  <TextField
                    label="From"
                    type="time"
                    required
                    value={block.start}
                    onChange={(e) => changeBlock(index, { start: e.target.value })}
                  />
                  <TextField
                    label="To"
                    type="time"
                    required
                    value={block.end}
                    onChange={(e) => changeBlock(index, { end: e.target.value })}
                  />
                  <TextField
                    label="Slot (min)"
                    type="number"
                    min={5}
                    max={240}
                    required
                    value={block.slotMinutes}
                    onChange={(e) => changeBlock(index, { slotMinutes: Number(e.target.value) })}
                  />
                  <Button
                    variant="dangerGhost"
                    size="sm"
                    aria-label="Remove hours"
                    onClick={() => setSchedule((blocks) => blocks.filter((_, i) => i !== index))}
                  >
                    Remove
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </fieldset>

        {error && <Alert message={error} />}

        <div className="flex justify-end gap-3 pt-1">
          <Button variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting || !scheduleLoaded}>
            {submitting ? "Saving..." : "Save"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
