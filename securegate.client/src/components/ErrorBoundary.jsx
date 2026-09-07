// Render xatolarini ushlaydigan chegara — bitta komponent yiqilsa ham
// butun ilova oq ekranga aylanmasin.
//
// Foydalanish:
//   <ErrorBoundary>{children}</ErrorBoundary>
//   <ErrorBoundary fallback={<MyFallback/>}>{children}</ErrorBoundary>
//   fallback funksiya ham bo'lishi mumkin: fallback={(error, retry) => ...}
import React from "react";
import { Icon } from "./Icon";

export default class ErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { error: null };
    this.retry = this.retry.bind(this);
  }

  static getDerivedStateFromError(error) {
    return { error };
  }

  componentDidCatch(error, info) {
    // Konsolga chiqaramiz — ishlab chiqishda diagnostika uchun.
    console.error("[ErrorBoundary]", error, info?.componentStack);
  }

  retry() {
    this.setState({ error: null });
  }

  render() {
    const { error } = this.state;
    if (!error) return this.props.children;

    const { fallback } = this.props;
    if (fallback !== undefined && fallback !== null) {
      return typeof fallback === "function" ? fallback(error, this.retry) : fallback;
    }

    return (
      <div className="card padded error-box" role="alert">
        <Icon name="alert" size={18} />
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontWeight: 500 }}>Ushbu bo'limda kutilmagan xatolik yuz berdi.</div>
          <div className="faint" style={{ fontSize: 11.5, marginTop: 2, wordBreak: "break-word" }}>
            {error?.message || "Noma'lum xatolik."}
          </div>
        </div>
        <button type="button" className="btn sm" onClick={this.retry}>
          <Icon name="refresh" size={13} /> Qayta urinish
        </button>
      </div>
    );
  }
}
