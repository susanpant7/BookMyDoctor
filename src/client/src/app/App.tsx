import { BrowserRouter, Route, Routes } from "react-router-dom";
import MainLayout from "@/app/MainLayout";
import AppointmentsPage from "@/features/appointments/AppointmentsPage";
import BookPage from "@/features/appointments/BookPage";
import AuthProvider from "@/features/auth/AuthProvider";
import LoginPage from "@/features/auth/LoginPage";
import ProtectedRoute from "@/features/auth/ProtectedRoute";
import { useAuth } from "@/features/auth/useAuth";
import ChatPage from "@/features/chats/ChatPage";
import DoctorsAdminPage from "@/features/doctors/DoctorsAdminPage";
import DoctorsPage from "@/features/doctors/DoctorsPage";
import HomePage from "@/features/home/HomePage";
import KnowledgePage from "@/features/knowledge/KnowledgePage";
import SettingsPage from "@/features/settings/SettingsPage";
import SpecialtiesPage from "@/features/specialties/SpecialtiesPage";
import UsersPage from "@/features/users/UsersPage";

// The admin manages the doctors; everyone else just sees them.
function Doctors() {
  const { isAdmin } = useAuth();
  return isAdmin ? <DoctorsAdminPage /> : <DoctorsPage />;
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route element={<MainLayout />}>
            <Route path="/" element={<HomePage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route element={<ProtectedRoute />}>
              <Route path="/doctors" element={<Doctors />} />
            </Route>
            <Route element={<ProtectedRoute role="Patient" />}>
              <Route path="/appointments" element={<AppointmentsPage />} />
              <Route path="/book" element={<BookPage />} />
              <Route path="/book/chat" element={<ChatPage />} />
            </Route>
            <Route element={<ProtectedRoute role="Admin" />}>
              <Route path="/users" element={<UsersPage />} />
              <Route path="/specialties" element={<SpecialtiesPage />} />
              <Route path="/knowledge" element={<KnowledgePage />} />
              <Route path="/settings" element={<SettingsPage />} />
            </Route>
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}
