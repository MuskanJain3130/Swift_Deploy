import React from "react";
import "../css/Projects.css";
import { NavigationBar } from "../Components/NavigationBar"; // Import the NavBar

const projects = [
  {
    name: "innovative-app",
    description: "A cutting-edge productivity app leveraging AI for automated workflows.",
    updated: "Updated 2 days ago",
    stars: 120,
  },
  {
    name: "data-insights-platform",
    description: "Advanced platform for big data analytics and visualization with Python and Spark.",
    updated: "Updated 1 week ago",
    stars: 85,
  },
  {
    name: "shop-backend",
    description: "Robust REST API for online shop operations, powered by Go and PostgreSQL.",
    updated: "Updated 3 weeks ago",
    stars: 210,
  },
  {
    name: "personal-site-v2",
    description: "Revamped personal website focusing on speed and accessibility with Next.js.",
    updated: "Updated 1 month ago",
    stars: 45,
  },
];

function Projects() {
  return (
    <div className="projects-page">
      {/* Left Sidebar */}
      <NavigationBar />

      {/* Main Content */}
      <div className="projects-main">
        <div className="header-row">
          <h2>Your Projects</h2>
          <button className="new-project-btn">+ New Project</button>
        </div>

        <div className="projects-grid">
          {projects.map((proj, idx) => (
            <div className="project-card" key={idx}>
              <div className="project-title">{proj.name}</div>
              <div className="project-desc">{proj.description}</div>
              <div className="project-info-row">
                <span className="project-updated">{proj.updated}</span>
                <span className="project-stars">⭐ {proj.stars}</span>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
export default Projects;
