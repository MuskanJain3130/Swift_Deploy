import React from "react";
import Header from "../Components/Header";
import Footer from "../Components/Footer";
import { FaGithub } from "react-icons/fa";
import { IoIosQrScanner } from "react-icons/io";
import { LuLightbulb, LuRocket, LuWrench} from "react-icons/lu";
import { HiOutlineLightningBolt } from "react-icons/hi";
import { FiGithub } from "react-icons/fi";
import { BsCurrencyDollar } from "react-icons/bs";
import { IoMdCode } from "react-icons/io";
import "../css/Landing.css";

const features = [
  {
    icon: <IoIosQrScanner className="feature-icon" />,
    title: "Detect Tech Stack",
    description: (
      <>
        Automatically analyze your repository<br />
        to identify the programming languages and frameworks used.
      </>
    ),
  },
  {
    icon: <LuLightbulb className="feature-icon" />,
    title: "Recommend Platforms",
    description: (
      <>
        Get intelligent suggestions for the best<br />
        deployment platforms tailored to your project.
      </>
    ),
  },
  {
    icon: <HiOutlineLightningBolt className="feature-icon" />,
    title: "Deploy Instantly",
    description: (
      <>
        Launch your applications with a single<br />
        click, streamlining your development workflow.
      </>
    ),
  },
];

const howWorks = [
  {
    icon: <FiGithub className="how-works-icon" />,
    title: "Connect Your Repository",
    description: (
      <>
        Seamlessly link your GitHub account<br />
        and select the repository you want to deploy.
      </>
    ),
  },
  {
    icon: <LuLightbulb className="how-works-icon" />,
    title: "Get Instant Recommendations",
    description: (
      <>
        Our AI analyzes your project's tech stack<br />
        and suggests the best deployment platforms.
      </>
    ),
  },
  {
    icon: <LuRocket className="how-works-icon" />,
    title: "Deploy with Confidence",
    description: (
      <>
        Launch your application with a single click<br />
        and monitor its status in real-time.
      </>
    ),
  },
];

const whyChoose = [
  {
    icon: <LuWrench className="feature-icon" />,
    title: "Simplify DevOps",
    description: (
      <>
        Eliminate complex configurations<br />
        and manual setups.
      </>
    ),
  },
  {
    icon: <HiOutlineLightningBolt className="feature-icon" />,
    title: "Accelerate Development",
    description: (
      <>
        Go from code to live in minutes,<br />
        <b>not</b> hours or days.
      </>
    ),
  },
  {
    icon: <BsCurrencyDollar className="feature-icon" />,
    title: "Cost-Efficient Deployments",
    description: (
      <>
        Optimize resource usage with smart<br />
        platform recommendations.
      </>
    ),
  },
  {
    icon: <IoMdCode className="feature-icon" />,
    title: "Focus on Your Code",
    description: (
      <>
        Spend less time on infrastructure,<br />
        more on building great features.
      </>
    ),
  },
];

const Landing = () => {
  return (
    <div className="landing-bg">
      <Header />
      <main className="landing-main">
        <h1 className="landing-title">One-click Deployments<br />from<br />GitHub Repos</h1>
        <div className="features-row">
          {features.map((feature, idx) => (
            <div className="feature-card" key={idx}>
              {feature.icon}
              <h2 className="feature-title">{feature.title}</h2>
              <p className="feature-desc">{feature.description}</p>
            </div>
          ))}
        </div>
        {/* How SwiftDeploy Works Section */}
        <section className="how-works-section">
          <h2 className="how-works-title">How SwiftDeploy Works</h2>
          <div className="how-works-row">
            {howWorks.map((item, idx) => (
              <div className="feature-card" key={idx}>
                {React.cloneElement(item.icon, { className: "feature-icon" })}
                <h3 className="feature-title">{item.title}</h3>
                <p className="feature-desc">{item.description}</p>
              </div>
            ))}
          </div>
        </section>
        {/* Why Choose SwiftDeploy Section */}
        <section className="why-choose-section">
          <h2 className="why-choose-title">Why Choose SwiftDeploy?</h2>
          <div className="why-choose-row">
            {whyChoose.map((item, idx) => (
              <div className="feature-card" key={idx}>
                {item.icon}
                <h3 className="feature-title">{item.title}</h3>
                <p className="feature-desc">{item.description}</p>
              </div>
            ))}
          </div>
          <div className="cta-section">
            <h2 className="cta-title">Ready to Simplify Your Deployments?</h2>
            <button className="cta-btn">
              <FaGithub style={{ marginRight: 8, fontSize: '1.2em' }} />
              Get Started with GitHub
            </button>
          </div>
        </section>
      </main>
      <Footer />
    </div>
  );
};

export default Landing;
