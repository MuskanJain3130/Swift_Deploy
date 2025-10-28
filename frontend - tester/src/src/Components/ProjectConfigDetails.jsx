import React from 'react';

const ProjectConfigDetails = ({ config }) => {
  console.log(config);
  return (
  <div className="card mb-4 shadow-sm">
    <div className="card-body">
      <h5 className="card-title">{config.projectName}</h5>
      <p className="card-text mb-2">
        <strong>Type:</strong> {config.framework}<br />
        <strong>Build Command:</strong> {config.buildCommand}<br />
        <strong>Output Directory:</strong> {config.outputDirectory}<br />
        <strong>Domain:</strong> {config.domain}<br />
      </p>
    </div>
  </div>
)};

export default ProjectConfigDetails;
