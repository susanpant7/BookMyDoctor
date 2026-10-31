import { Outlet } from "react-router-dom";
import Navbar from "@/shared/components/Navbar";

export default function MainLayout() {
  return (
    <div className="min-h-screen bg-stone-50 text-stone-900">
      <Navbar />
      <Outlet />
    </div>
  );
}
