import React from "react";
import { MdOutlineDashboard } from "react-icons/md";
import { CiSettings } from "react-icons/ci";
import { FaTerminal } from "react-icons/fa6";
import { PiGitForkLight } from "react-icons/pi";
import { GoQuestion } from "react-icons/go";
import { FiLogOut } from "react-icons/fi";

import "../css/NavigationBar.css";

export const NavigationBar = () => {
  return (
    <div className="background">
      <div className="margin mt-4">
        <img src="/logo.ico" alt="Logo" className="logo" />
        <div className="container">
          <div className="top-icons">
            <div className="iconify-icon mb-4"><MdOutlineDashboard /></div>
            <div className="iconify-icon mb-4"><PiGitForkLight /></div>
            <div className="iconify-icon mb-4"><FaTerminal /></div>
            <div className="iconify-icon"><CiSettings /></div>
          </div>
          <div className="bottom-icons">
            <div className="iconify-icon"><GoQuestion /></div>
            <div className="iconify-icon"><FiLogOut /></div>
          </div>
        </div>
      </div>
    </div>
  );
};