// src/components/DeploymentPlatform.jsx
import React from 'react';
import { Card, Button, Badge, Spinner } from 'react-bootstrap';
import { 
  Github as GitHubIcon,
  Cloud as CloudIcon,
  CheckCircle,
  XCircle,
  Clock,
  ArrowRight
} from 'react-bootstrap-icons';

const platformConfig = {
  github: {
    name: 'GitHub Pages',
    icon: <GitHubIcon className="me-2" />,
    color: 'dark'
  },
  netlify: {
    name: 'Netlify',
    icon: <CloudIcon className="me-2" />,
    color: 'info'
  },
  vercel: {
    name: 'Vercel',
    icon: <CloudIcon className="me-2" />,
    color: 'secondary'
  },
  cloudflare: {
    name: 'Cloudflare Pages',
    icon: <CloudIcon className="me-2" />,
    color: 'warning'
  }
};

const DeploymentPlatform = ({ platform, status, onDeploy, loading }) => {
  const config = platformConfig[platform] || {
    name: platform,
    icon: <CloudIcon className="me-2" />,
    color: 'secondary'
  };

  const getStatusBadge = () => {
    if (loading) {
      return (
        <Badge bg="light" text="dark" className="d-flex align-items-center">
          <Spinner animation="border" size="sm" className="me-1" />
          Deploying...
        </Badge>
      );
    }

    if (!status) return null;

    switch (status.state) {
      case 'success':
        return (
          <Badge bg="success" className="d-flex align-items-center">
            <CheckCircle className="me-1" /> Deployed
          </Badge>
        );
      case 'error':
        return (
          <Badge bg="danger" className="d-flex align-items-center">
            <XCircle className="me-1" /> Failed
          </Badge>
        );
      case 'pending':
        return (
          <Badge bg="warning" text="dark" className="d-flex align-items-center">
            <Clock className="me-1" /> Pending
          </Badge>
        );
      default:
        return null;
    }
  };

  return (
    <Card className="h-100">
      <Card.Body className="d-flex flex-column">
        <div className="d-flex justify-content-between align-items-start mb-3">
          <div>
            <h5 className={`h6 mb-1 text-${config.color}`}>
              {config.icon}
              {config.name}
            </h5>
            <p className="text-muted small mb-0">
              {platform === 'github' 
                ? 'Host directly from your GitHub repository' 
                : `Deploy to ${config.name}`}
            </p>
          </div>
          {getStatusBadge()}
        </div>

        <div className="mt-auto">
          {status?.url && (
            <div className="mb-3">
              <a 
                href={status.url} 
                target="_blank" 
                rel="noopener noreferrer" 
                className="btn btn-outline-secondary btn-sm w-100 mb-2 d-flex align-items-center justify-content-center"
              >
                View Deployment <ArrowRight className="ms-1" />
              </a>
            </div>
          )}

          <Button
            variant={status?.state === 'success' ? 'outline-success' : 'primary'}
            className="w-100"
            onClick={() => onDeploy(platform)}
            disabled={loading}
          >
            {loading ? (
              <>
                <Spinner
                  as="span"
                  animation="border"
                  size="sm"
                  role="status"
                  aria-hidden="true"
                  className="me-2"
                />
                Deploying...
              </>
            ) : status?.state === 'success' ? (
              'Redeploy'
            ) : (
              `Deploy to ${config.name}`
            )}
          </Button>

          {status?.lastDeployed && (
            <div className="text-muted small mt-2">
              Last deployed: {new Date(status.lastDeployed).toLocaleString()}
            </div>
          )}
        </div>
      </Card.Body>
    </Card>
  );
};

export default DeploymentPlatform;