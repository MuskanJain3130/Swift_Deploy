import React from 'react';
import '../css/Footer.css';

const Footer = () => {
  return (
    <footer className="footer">
      <div className="footer-links">
        <a href="/privacy" className="footer-link">Privacy Policy</a>
        <a href="/terms" className="footer-link">Terms of Service</a>
        <a href="/contact" className="footer-link">Contact Us</a>
      </div>
      <div className="footer-right">
        <div>© 2025 SwiftDeploy All rights reserved.</div>
      </div>
    </footer>
  );
};

export default Footer;
