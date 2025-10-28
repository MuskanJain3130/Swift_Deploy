import React from 'react';
import DeploymentPlatformCard from './DeploymentPlatformCard';

const platforms = [
  {
    name: "Vercel",
    description: "Ideal for Next.js, React, and static sites. Offers global CDN and serverless functions.",
    freeTier: true
  },
  {
    name: "Github Pages",
    description: "Supports static sites and JAMstack apps",
    freeTier: true
  },
  {
    name: "Netlify",
    description: "Perfect for static sites and JAMstack apps. Features include serverless functions and forms.",
    freeTier: true
  },
  {
    name: "Cloudflare Pages",
    description: "Supports static sites and JAMstack apps.",
    freeTier: true
  }
];

const DeploymentPlatformOptions = () => (
  <div className="row g-4">
    {platforms.map(platform => (
      <div className="col-md-3" key={platform.name}>
        <DeploymentPlatformCard platform={platform} />
      </div>
    ))}
  </div>
);

export default DeploymentPlatformOptions;
