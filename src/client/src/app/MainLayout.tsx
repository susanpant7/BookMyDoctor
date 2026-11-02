import { Outlet } from "react-router-dom";
import Navbar from "@/shared/components/Navbar";
import ScrollToTopButton from "@/shared/components/ScrollToTopButton";

export default function MainLayout() {
  return (
    <div className="min-h-screen bg-stone-50 text-stone-900">
      <Navbar />
      <Outlet />
      <ScrollToTopButton />
    </div>
  );
}
