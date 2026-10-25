import { Navigate, Route, Routes } from "react-router-dom";
import { AppointmentsPage } from "./features/appointments/AppointmentsPage";
import { BookPage } from "./features/appointments/BookPage";
import { AuthPage } from "./features/auth/AuthPage";
import { DoctorsPage } from "./features/doctors/DoctorsPage";
import { Navbar } from "./shared/Navbar";
import { useSession } from "./shared/session";

export function App() {
  const { session } = useSession();

  return (
    <>
      <Navbar />
      <main className="app">
        <Routes>
          <Route path="/" element={session ? <Navigate to="/doctors" replace /> : <AuthPage />} />
          <Route path="/doctors" element={<DoctorsPage />} />
          <Route path="/appointments" element={<AppointmentsPage />} />
          <Route path="/book" element={<BookPage />} />
        </Routes>
      </main>
    </>
  );
}
