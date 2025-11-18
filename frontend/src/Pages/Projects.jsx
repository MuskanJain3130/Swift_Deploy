import React, { useState, useEffect } from "react";
import "../css/Projects.css";
import { NavigationBar } from "../Components/NavigationBar";
import { useNavigate } from "react-router-dom";

/* Example static projects (API projects grid) */

/* ----- Custom Dropdown Component ----- */
function RepoDropdown({ repos, selectedRepo, onSelect }) {
  const [open, setOpen] = useState(false);

  // Show only the repo name in closed state
  const displayName = selectedRepo ? selectedRepo.split("/")[1] : "-- Select a repo --";

  return (
    <div className="custom-dropdown">
      <div
        className="dropdown-selected"
        onClick={() => setOpen(!open)}
      >
        {displayName}
        <span className="dropdown-arrow">▼</span>
      </div>

      {open && (
        <div className="dropdown-list">
          {repos.map((repo) => (
            <div
              key={repo.id}
              className="dropdown-item"
              onClick={() => {
                onSelect(`${repo.owner.login}/${repo.name}`);
                setOpen(false);
              }}
            >
              {repo.name}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}


function Projects() {
  const [projects, setProjects] = useState([]); // Initially empty
  const [modalOpen, setModalOpen] = useState(false);
  const [repos, setRepos] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [selectedRepo, setSelectedRepo] = useState("");

  const navigate = useNavigate();

  /* Fetch repos when modal opens */
  useEffect(() => {
    const fetchRepositories = async () => {
      console.log('Fetching repositories...');
      const jwtToken = localStorage.getItem("jwtToken");
      console.log('Current JWT token from localStorage:', jwtToken ? 'Token exists' : 'No token found');
      
      if (!jwtToken) {
        const errorMsg = 'No authentication token found. Please log in again.';
        console.error(errorMsg);
        setError("Session expired. Please log in again.");
        setLoading(false);
        // Close the modal and navigate after a short delay to show the error
        setTimeout(() => {
          console.log('Closing modal and navigating to login...');
          setModalOpen(false);
          navigate("/");
        }, 1500);
        return;
      }

      try {
        console.log('Making API request to fetch repositories...');
        const apiUrl = "http://localhost:5280/api/repositories";
        console.log('API URL:', apiUrl);
        
        const response = await fetch(apiUrl, {
          method: "GET",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${jwtToken}`,
          },
        });
        
        console.log('API Response status:', response.status);

        if (!response.ok) {
          const errorText = await response.text();
          if (response.status === 401) {
            throw new Error("Session expired. Please log in again.");
          }
          throw new Error(`Failed to fetch repositories: ${response.status} ${errorText}`);
        }

        const data = await response.json();
        console.log('Repositories received:', data);
        setRepos(data);
      } catch (err) {
        console.error("Failed to fetch repositories:", err);
        setError(err.message);
        
        if (err.message.includes("Session expired") || err.message.includes("Unauthorized")) {
          // Clear the token and show error before navigating
          localStorage.removeItem("github_access_token");
          
          // Close the modal and show error before navigating
          setModalOpen(false);
          
          // Show error briefly before navigating
          setTimeout(() => {
            navigate("/", { state: { authError: err.message } });
          }, 1500);
        }
      } finally {
        setLoading(false);
      }
    };

    if (modalOpen) {
      setLoading(true);
      setError(null);
      fetchRepositories();
    }
  }, [modalOpen, navigate]);

  /* Add project directly to state */
 const handleAddProject = () => {
  if (!selectedRepo) return alert("Please select a repository");

  const [owner, repoName] = selectedRepo.split("/");
  const repoObj = repos.find(r => r.owner.login === owner && r.name === repoName); // ✅ fix comparison

  if (!repoObj) return alert("Repository not found");

  const newProject = {
    name: repoObj.name,
    description: repoObj.description || "No description provided.",
    updated: `Added just now`,
    stars: repoObj.stargazers_count || 0, // GitHub API uses stargazers_count
  };

  setProjects(prevProjects => [...prevProjects, newProject]);
  setModalOpen(false);
  setSelectedRepo("");
};


  return (
    <div className="projects-page">
      <NavigationBar />

      <div className="projects-main">
        <div className="header-row">
          <h2>Your Projects</h2>
          <button className="new-project-btn" onClick={() => setModalOpen(true)}>
            + New Project
          </button>
        </div>

        <div className="projects-grid">
          {projects.length === 0 ? (
            <p>No projects yet. Click "+ New Project" to add one.</p>
          ) : (
            projects.map((proj, idx) => (
              <div className="project-card" key={idx}>
                <div className="project-title">{proj.name}</div>
                <div className="project-desc">{proj.description}</div>
                <div className="project-info-row">
                  <span className="project-updated">{proj.updated}</span>
                  <span className="project-stars">⭐ {proj.stars}</span>
                </div>
              </div>
            ))
          )}
        </div>

        {/* Modal */}
        {modalOpen && (
          <div className="modal-overlay">
            <div className="modal-card">
              <h3>Select Repository for New Project</h3>

              {loading && <p>Loading repositories...</p>}
              {error && <p className="error-text">{error}</p>}

              {!loading && !error && (
                <RepoDropdown
                  repos={repos}
                  selectedRepo={selectedRepo}
                  onSelect={setSelectedRepo}
                />
              )}

              <div className="modal-buttons">
                <button onClick={handleAddProject} disabled={!selectedRepo}>
                  Add Project
                </button>
                <button onClick={() => setModalOpen(false)}>Cancel</button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export default Projects;