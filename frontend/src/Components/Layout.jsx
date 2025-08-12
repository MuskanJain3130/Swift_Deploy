import React from "react";
import { Outlet, useLocation } from "react-router-dom";
import { NavigationBar } from "./NavigationBar";
import Footer from "./Footer"; // If you want footer on every page
import "../App.css"// Assuming you have a CSS file for layout style

export default function Layout() {
  const location = useLocation();
  const hideNavbar = location.pathname === "/"; // Hide only on landing

  return (
    <div className="app-layout">
      {!hideNavbar && <NavigationBar />}
      <div className="main-content">
        <Outlet />
      </div>
    </div>
  );
}
