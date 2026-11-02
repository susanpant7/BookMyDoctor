import { Menu, Stethoscope, X } from "lucide-react";
import { useEffect, useState } from "react";
import { Link, NavLink, useLocation } from "react-router-dom";
import { useAuth } from "@/features/auth/useAuth";
import Button from "@/shared/components/Button";
import { buttonClass } from "@/shared/components/buttonStyles";

type NavItem = { to: string; label: string; end?: boolean };

const publicLinks: NavItem[] = [
  { to: "/", label: "Home", end: true },
  { to: "/docs", label: "Dev Docs" }
];
const signedInLinks: NavItem[] = [{ to: "/doctors", label: "Doctors" }];
const patientLinks: NavItem[] = [
  { to: "/appointments", label: "Appointments" },
  { to: "/book", label: "Book" }
];
const doctorLinks: NavItem[] = [{ to: "/doctor/appointments", label: "My appointments" }];
const adminLinks: NavItem[] = [
  { to: "/users", label: "Patients" },
  { to: "/specialties", label: "Specialties" },
  { to: "/knowledge", label: "Knowledge" },
  { to: "/settings", label: "Settings" }
];

const linkClass = ({ isActive }: { isActive: boolean }) =>
  `whitespace-nowrap rounded-lg px-3 py-2 text-sm font-medium transition ${
    isActive ? "bg-brand-50 text-brand-700" : "text-stone-600 hover:bg-stone-100 hover:text-stone-900"
  }`;

export default function Navbar() {
  const { user, isAdmin, isPatient, isDoctor, logout } = useAuth();
  // The menu of a small screen. It closes when another page opens.
  const [open, setOpen] = useState(false);
  const location = useLocation();
  useEffect(() => setOpen(false), [location.pathname]);

  const links: NavItem[] = [
    ...publicLinks,
    ...(user ? signedInLinks : []),
    ...(isPatient ? patientLinks : []),
    ...(isDoctor ? doctorLinks : []),
    ...(isAdmin ? adminLinks : [])
  ];

  const avatar = user && (
    <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-brand-100 text-sm font-semibold text-brand-700">
      {user.name[0].toUpperCase()}
    </span>
  );

  return (
    <header className="sticky top-0 z-20 border-b border-stone-200 bg-white/80 backdrop-blur">
      <nav className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-4 sm:px-6">
        <div className="flex min-w-0 items-center gap-8">
          <Link to="/" className="flex shrink-0 items-center gap-2.5">
            <span className="flex size-8 items-center justify-center rounded-lg bg-brand-600 text-white">
              <Stethoscope className="size-5" aria-hidden="true" />
            </span>
            <span className="text-lg font-semibold tracking-tight text-stone-900">MyHospitalAI</span>
          </Link>
          <div className="hidden items-center gap-1 lg:flex">
            {links.map(({ to, label, end }) => (
              <NavLink key={to} to={to} end={end} className={linkClass}>
                {label}
              </NavLink>
            ))}
          </div>
        </div>

        {/* Large screens: the user and Sign out (or Sign in) next to the links. */}
        <div className="hidden shrink-0 items-center gap-3 lg:flex">
          {user ? (
            <>
              {avatar}
              <div className="hidden leading-tight xl:block">
                <p className="text-sm font-medium text-stone-900">{user.name}</p>
                <p className="text-xs text-stone-500">{user.role}</p>
              </div>
              <Button variant="secondary" size="sm" onClick={logout}>
                Sign out
              </Button>
            </>
          ) : (
            <Link to="/login" className={buttonClass("primary", "sm")}>
              Sign in
            </Link>
          )}
        </div>

        {/* Small screens: a menu button. */}
        <button
          type="button"
          aria-label={open ? "Close the menu" : "Open the menu"}
          aria-expanded={open}
          aria-controls="mobile-menu"
          onClick={() => setOpen((value) => !value)}
          className="flex size-10 items-center justify-center rounded-lg text-stone-700 hover:bg-stone-100 lg:hidden"
        >
          {open ? <X className="size-6" aria-hidden="true" /> : <Menu className="size-6" aria-hidden="true" />}
        </button>
      </nav>

      {open && (
        <div id="mobile-menu" className="border-t border-stone-200 bg-white px-4 pb-4 pt-2 shadow-sm sm:px-6 lg:hidden">
          <div className="flex flex-col gap-1">
            {links.map(({ to, label, end }) => (
              <NavLink key={to} to={to} end={end} className={linkClass}>
                {label}
              </NavLink>
            ))}
          </div>
          <div className="mt-3 border-t border-stone-200 pt-3">
            {user ? (
              <div className="flex items-center justify-between gap-3">
                <div className="flex min-w-0 items-center gap-3">
                  {avatar}
                  <div className="min-w-0 leading-tight">
                    <p className="truncate text-sm font-medium text-stone-900">{user.name}</p>
                    <p className="text-xs text-stone-500">{user.role}</p>
                  </div>
                </div>
                <Button variant="secondary" size="sm" onClick={logout}>
                  Sign out
                </Button>
              </div>
            ) : (
              <Link to="/login" className={`${buttonClass("primary", "sm")} w-full`}>
                Sign in
              </Link>
            )}
          </div>
        </div>
      )}
    </header>
  );
}
