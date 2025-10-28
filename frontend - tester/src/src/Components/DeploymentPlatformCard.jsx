import React from 'react';

const DeploymentPlatformCard = ({ platform }) => (
  <div className="card h-100 deployment-card">
    <div className="card-body">
      <h5 className="card-title">{platform.name}</h5>
      <span className={`badge rounded-pill bg-${platform.freeTier ? 'success' : 'secondary'} mb-2`}>
        {platform.freeTier ? "FREE TIER AVAILABLE" : "PAID"}
      </span>
      <p className="card-text">{platform.description}</p>
      <button type="button" className="btn btn-primary">Deploy Now</button>
    </div>
  </div>
);

export default DeploymentPlatformCard;
