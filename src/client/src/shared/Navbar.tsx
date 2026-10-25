import { useState } from "react";
import { Link, NavLink, useNavigate } from "react-router-dom";
import {
  CalendarDays,
  CalendarPlus,
  LogIn,
  LogOut,
  Menu,
  Stethoscope,
  UserRound,
  X
} from "lucide-react";
import { useSession } from "./session";

export function Navbar() {
  const { session, setSession } = useSession();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);

  function signOut() {
    setSession(null);
    setOpen(false);
    navigate("/");
  }

  return (
    <header className="navbar">
      <div className="navbar-inner">
        <Link to={session ? "/doctors" : "/"} className="brand" onClick={() => setOpen(false)}>
          <Stethoscope size={20} aria-hidden="true" />
          BookMyDoctor
        </Link>
        <button
          type="button"
          className="navbar-toggle"
          aria-expanded={open}
          aria-label={open ? "Close navigation" : "Open navigation"}
          onClick={() => setOpen((current) => !current)}
        >
          {open ? <X size={20} aria-hidden="true" /> : <Menu size={20} aria-hidden="true" />}
        </button>
        <nav className={open ? "navbar-links open" : "navbar-links"}>
          <NavLink to="/doctors" onClick={() => setOpen(false)}>
            <UserRound size={16} aria-hidden="true" />
            Doctors
          </NavLink>
          <NavLink to="/appointments" onClick={() => setOpen(false)}>
            <CalendarDays size={16} aria-hidden="true" />
            Appointments
          </NavLink>
          <NavLink to="/book" onClick={() => setOpen(false)}>
            <CalendarPlus size={16} aria-hidden="true" />
            Book
          </NavLink>
          {session ? (
            <span className="navbar-user">
              <span className="muted">{session.name}</span>
              <button type="button" className="link" onClick={signOut}>
                <LogOut size={16} aria-hidden="true" />
                Sign out
              </button>
            </span>
          ) : (
            <NavLink to="/" end onClick={() => setOpen(false)}>
              <LogIn size={16} aria-hidden="true" />
              Sign in
            </NavLink>
          )}
        </nav>
      </div>
    </header>
  );
}
