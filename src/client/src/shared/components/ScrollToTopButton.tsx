import { ArrowUp } from "lucide-react";
import { useEffect, useState } from "react";

// How far down the page has to be scrolled before the button shows.
const SHOW_AFTER = 300;

/** A round button at the bottom right that scrolls the page back to the top. Hidden while the page is near the top. */
export default function ScrollToTopButton() {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const update = () => setVisible(window.scrollY > SHOW_AFTER);
    update();
    window.addEventListener("scroll", update, { passive: true });
    return () => window.removeEventListener("scroll", update);
  }, []);

  if (!visible) return null;

  return (
    <button
      type="button"
      aria-label="Scroll to the top"
      title="Back to the top"
      onClick={() => window.scrollTo({ top: 0, behavior: "smooth" })}
      className="fixed bottom-6 right-6 z-30 flex size-11 items-center justify-center rounded-full bg-brand-600 text-white shadow-lg transition hover:bg-brand-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 focus-visible:ring-offset-2"
    >
      <ArrowUp className="size-5" aria-hidden="true" />
    </button>
  );
}
