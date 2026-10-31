import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "@/features/auth/useAuth";

// Pages for signed-in users, optionally only one role. Someone who is signed out goes to the sign-in page,
// and the wrong role goes home.
export default function ProtectedRoute({ role }: { role?: "Admin" | "Patient" }) {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  return !role || user.role === role ? <Outlet /> : <Navigate to="/" replace />;
}
