import React from "react";
import "../css/Header.css";
import { FaGithub } from "react-icons/fa";

const Header = () => {
  return (
    <header className="header">
      <div className="header-left">
        <img src={process.env.PUBLIC_URL + "/logo.ico"} alt="SwiftDeploy Logo" className="logo" />
        <span className="title">SwiftDeploy</span>
      </div>
      <div className="header-right">
        <button className="github-login">
          <FaGithub className="github-icon" />
          Login with GitHub
        </button>
      </div>
    </header>
  );
};

export default Header;
