import React, { useState, useEffect } from 'react';
import ProjectConfigDetails from '../Components/ProjectConfigDetails';
import DeploymentPlatformOptions from '../Components/DeploymentPlatformOptions';
import '../css/ProjectDeployments.css';

const ProjectDeploymentConfigPanel = () => { 
  const urlParams = new URLSearchParams(window.location.search);
  const projectName = urlParams.get('projectName');
  const [config, setConfig] = useState({
    projectName: "",
    framework: "",
    buildCommand: "",
    environmentVariables: "",
    domain: ""
  });
  
  useEffect(() => {
    const fetchProjectConfig = async () => {
      try {
        const response = await fetch(
          `${import.meta.env.REACT_APP_API_BASE_URL}/projects/${projectName}`
        );
        if (!response.ok) {
          throw new Error('Failed to fetch project config');
        }
        const data = await response.json();
        setConfig(data);
      } catch (error) {
        console.error('Error fetching project config:', error);
      }
    };
    
    if (projectName) {
      fetchProjectConfig();
    }
  }, [projectName]);

  return (
    <div className="p-5 m-0 deployment-config">
      <h2 className="mb-3 text-light">Config Details</h2>
      <ProjectConfigDetails config={config} />
      <h2 className="mb-3 text-light">Deployment Platform Options</h2>
      <DeploymentPlatformOptions />
    </div>
  );
};

export default ProjectDeploymentConfigPanel;
