import { Stethoscope } from "lucide-react";
import { Link, NavLink } from "react-router-dom";
import { useAuth } from "@/features/auth/useAuth";
import Button from "@/shared/components/Button";
import { buttonClass } from "@/shared/components/buttonStyles";

type NavItem = { to: string; label: string; end?: boolean };

const publicLinks: NavItem[] = [{ to: "/", label: "Home", end: true }];
const signedInLinks: NavItem[] = [{ to: "/doctors", label: "Doctors" }];
const patientLinks: NavItem[] = [
  { to: "/appointments", label: "Appointments" },
  { to: "/book", label: "Book" }
];
const adminLinks: NavItem[] = [
  { to: "/users", label: "Patients" },
  { to: "/specialties", label: "Specialties" },
  { to: "/knowledge", label: "Knowledge" },
  { to: "/settings", label: "Settings" }
];

export default function Navbar() {
  const { user, isAdmin, isPatient, logout } = useAuth();
  const links: NavItem[] = [
    ...publicLinks,
    ...(user ? signedInLinks : []),
    ...(isPatient ? patientLinks : []),
    ...(isAdmin ? adminLinks : [])
  ];

  return (
    <header className="sticky top-0 z-20 border-b border-stone-200 bg-white/80 backdrop-blur">
      <nav className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-6">
        <div className="flex min-w-0 items-center gap-8">
          <Link to="/" className="flex shrink-0 items-center gap-2.5">
            <span className="flex size-8 items-center justify-center rounded-lg bg-brand-600 text-white">
              <Stethoscope className="size-5" aria-hidden="true" />
            </span>
            <span className="text-lg font-semibold tracking-tight text-stone-900">BookMyDoctor</span>
          </Link>
          <div className="flex items-center gap-1 overflow-x-auto">
            {links.map(({ to, label, end }) => (
              <NavLink
                key={to}
                to={to}
                end={end}
                className={({ isActive }) =>
                  `whitespace-nowrap rounded-lg px-3 py-2 text-sm font-medium transition ${
                    isActive ? "bg-brand-50 text-brand-700" : "text-stone-600 hover:bg-stone-100 hover:text-stone-900"
                  }`
                }
              >
                {label}
              </NavLink>
            ))}
          </div>
        </div>

        {user ? (
          <div className="flex shrink-0 items-center gap-3">
            <span className="flex size-8 items-center justify-center rounded-full bg-brand-100 text-sm font-semibold text-brand-700">
              {user.name[0].toUpperCase()}
            </span>
            <div className="hidden leading-tight sm:block">
              <p className="text-sm font-medium text-stone-900">{user.name}</p>
              <p className="text-xs text-stone-500">{user.role}</p>
            </div>
            <Button variant="secondary" size="sm" onClick={logout}>
              Sign out
            </Button>
          </div>
        ) : (
          <Link to="/login" className={buttonClass("primary", "sm")}>
            Sign in
          </Link>
        )}
      </nav>
    </header>
  );
}
