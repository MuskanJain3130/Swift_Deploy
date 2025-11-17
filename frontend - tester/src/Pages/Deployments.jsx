import React, { useState } from "react";

export default function Deployments() {
  const [repo, setRepo] = useState("");
  const [branch, setBranch] = useState("main");
  const [message, setMessage] = useState("");

  const handleDeploy = async () => {
    try {
      const res = await fetch("http://localhost:5280/api/netlify/deploy", {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ repo: repo, branch: branch }),
    });

      const data = await res.json();
      if (res.ok) {
        setMessage(`✅ Deployment started. Site: ${data.site_url}`);
      } else if (data.needs_github_install) {
        window.location.href = data.install_url; // send user to install GitHub App
      } else {
        setMessage(`❌ Error: ${data.message} (${data.error})`);
      }

    } catch (err) {
      setMessage("❌ Failed: " + err.message);
    }
  };

  return (
    <div style={{ padding: "20px" }}>
      <h2>Deploy to Netlify</h2>
      <div>
        <label>Repo (username/repo): </label>
        <input value={repo} onChange={(e) => setRepo(e.target.value)} />
      </div>
      <div>
        <label>Branch: </label>
        <input value={branch} onChange={(e) => setBranch(e.target.value)} />
      </div>
      <button onClick={handleDeploy}>Deploy</button>
      <p>{message}</p>
    </div>
  );
}
