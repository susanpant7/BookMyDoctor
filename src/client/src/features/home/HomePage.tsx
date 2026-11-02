import { Bot, CalendarCheck, Stethoscope } from "lucide-react";
import { Link } from "react-router-dom";
import { useAuth } from "@/features/auth/useAuth";
import { buttonClass } from "@/shared/components/buttonStyles";

const features = [
  { icon: Stethoscope, title: "Find the right doctor", text: "Browse specialists, their experience, languages and fees." },
  { icon: Bot, title: "Ask Vaidya Mitra", text: "Describe what is wrong and the assistant suggests a doctor and finds a free time." },
  { icon: CalendarCheck, title: "Book in seconds", text: "Pick a time, confirm, and get a booking reference." }
];

export default function HomePage() {
  const { user, isPatient, isAdmin, isDoctor } = useAuth();

  return (
    <main className="mx-auto max-w-6xl px-4 py-12 sm:px-6 sm:py-16">
      <section className="text-center">
        <h1 className="text-4xl font-bold tracking-tight text-stone-900 sm:text-5xl">Book your doctor, without the waiting</h1>
        <p className="mx-auto mt-4 max-w-2xl text-lg text-stone-600">
          Mero Aspataal: see our doctors, chat with an assistant that knows the hospital, and book a visit in minutes.
        </p>
        <div className="mt-8 flex flex-wrap justify-center gap-3">
          {user && (
            <Link to="/doctors" className={buttonClass("primary", "lg")}>
              See our doctors
            </Link>
          )}
          {isPatient && (
            <Link to="/book" className={buttonClass("secondary", "lg")}>
              Book an appointment
            </Link>
          )}
          {isDoctor && (
            <Link to="/doctor/appointments" className={buttonClass("secondary", "lg")}>
              See my appointments
            </Link>
          )}
          {isAdmin && (
            <Link to="/users" className={buttonClass("secondary", "lg")}>
              Open the admin pages
            </Link>
          )}
          {!user && (
            <Link to="/login" className={buttonClass("primary", "lg")}>
              Sign in to see our doctors
            </Link>
          )}
        </div>
      </section>

      <section className="mt-16 grid gap-5 sm:grid-cols-3">
        {features.map(({ icon: Icon, title, text }) => (
          <div key={title} className="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm sm:p-6">
            <span className="flex size-10 items-center justify-center rounded-lg bg-brand-50 text-brand-600">
              <Icon className="size-5" aria-hidden="true" />
            </span>
            <h2 className="mt-4 font-semibold text-stone-900">{title}</h2>
            <p className="mt-1 text-sm leading-6 text-stone-600">{text}</p>
          </div>
        ))}
      </section>
    </main>
  );
}
