import { useEffect, useRef, useState } from "react";
import { Oval } from 'react-loader-spinner'
import { useDispatch, useSelector } from 'react-redux';
import { Alert } from "reactstrap";
import { testChaptarrSettings as testSettings } from "../../../store/actions/ChaptarrClientActions"
import { setChaptarrConnectionSettings as setConnectionSettings } from "../../../store/actions/ChaptarrClientActions"
import ValidatedTextbox from "../../Inputs/ValidatedTextbox"
import Textbox from "../../Inputs/Textbox"
import Dropdown from "../../Inputs/Dropdown"

import {
  FormGroup,
  Input,
  Row,
  Col
} from "reactstrap";


function Chaptarr(props) {
  const [isTestingSettings, setIsTestingSettings] = useState(false);
  const [testSettingsRequested, setTestSettingsRequested] = useState(false);
  const [testSettingsSuccess, setTestSettingsSuccess] = useState(false);
  const [testSettingsError, setTestSettingsError] = useState("");
  const [hostname, setHostname] = useState("");
  const [isHostnameValid, setIsHostnameValid] = useState(false);
  const [port, setPort] = useState("8789");
  const [isPortValid, setIsPortValid] = useState(false);
  const [apiKey, setApiKey] = useState("");
  const [isApiKeyValid, setIsApiKeyValid] = useState(false);
  const [useSSL, setUseSSL] = useState(false);
  const [baseUrl, setBaseUrl] = useState("");
  const [rootFolder, setRootFolder] = useState("");
  const [searchNewRequests, setSearchNewRequests] = useState(true);
  const [searchProvider, setSearchProvider] = useState("goodreads");

  const propRef = useRef();

  const reduxState = useSelector((state) => {
    return {
      settings: state.books.chaptarr
    }
  });
  const dispatch = useDispatch();


  useEffect(() => {
    updateStateFromProps()
  }, []);


  useEffect(() => {
    onValueChange();
  }, [apiKey, hostname, port, baseUrl, rootFolder, searchNewRequests, searchProvider]);


  // The connection fields validate themselves after load; re-check the form so saving works without an edit.
  useEffect(() => {
    onValidate();
  }, [isApiKeyValid, isHostnameValid, isPortValid]);



  const validateNonEmptyString = (value) => {
    return /\S/.test(value);
  };

  const validatePort = (value) => {
    return /^([0-9]{1,4}|[1-5][0-9]{4}|6[0-4][0-9]{3}|65[0-4][0-9]{2}|655[0-2][0-9]|6553[0-5])$/.test(value);
  };


  const updateStateFromProps = () => {
    setIsTestingSettings(false);
    setTestSettingsRequested(false);
    setTestSettingsSuccess(false);
    setTestSettingsError("");
    setHostname(reduxState.settings.hostname);
    setIsHostnameValid(false);
    setPort(reduxState.settings.port);
    setIsPortValid(false);
    setApiKey(reduxState.settings.apiKey);
    setIsApiKeyValid(false);
    setUseSSL(reduxState.settings.useSSL);
    setBaseUrl(reduxState.settings.baseUrl);
    setRootFolder(reduxState.settings.rootFolder);
    setSearchNewRequests(reduxState.settings.searchNewRequests);
    setSearchProvider(reduxState.settings.searchProvider ?? "goodreads");
  };

  const onUseSSLChanged = (event) => {
    setUseSSL(!useSSL);
    onValueChange();
  };


  const onTestSettings = (e) => {
    e.preventDefault();

    if (!isTestingSettings
      && isHostnameValid
      && isPortValid
      && isApiKeyValid) {
      setIsTestingSettings(true);

      dispatch(testSettings({
        hostname: hostname,
        baseUrl: baseUrl,
        port: port,
        apiKey: apiKey,
        useSSL: useSSL
      }))
        .then(data => {
          if (data.ok) {
            setTestSettingsRequested(true);
            setTestSettingsError("");
            setTestSettingsSuccess(true);
          } else {
            let error = "An unknown error occurred while testing the settings";

            if (typeof (data.error) === "string")
              error = data.error;

            setTestSettingsRequested(true);
            setTestSettingsError(error);
            setTestSettingsSuccess(false);
          }

          setIsTestingSettings(false);
        });
    }
  }

  const onValueChange = () => {
    dispatch(setConnectionSettings({
      hostname: hostname,
      baseUrl: baseUrl,
      port: port,
      apiKey: apiKey,
      useSSL: useSSL,
      rootFolder: rootFolder,
      searchNewRequests: searchNewRequests,
      searchProvider: searchProvider
    }));

    props.onChange({
      hostname: hostname,
      baseUrl: baseUrl,
      port: port,
      apiKey: apiKey,
      useSSL: useSSL,
      rootFolder: rootFolder,
      searchNewRequests: searchNewRequests,
      searchProvider: searchProvider
    });

    onValidate();
  };

  const onValidate = () => {
    props.onValidate(
      isApiKeyValid
      && isHostnameValid
      && isPortValid);
  };


  return (
    <>
      <div>
        <h6 className="heading-small text-muted mb-4">
          Chaptarr Connection Settings
        </h6>
      </div>
      <div className="pl-lg-4">
        <Row>
          <Col lg="6">
            <ValidatedTextbox
              name="API Key"
              placeholder="Enter api key"
              alertClassName="mt-3"
              errorMessage="api key is required."
              isSubmitted={props.isSubmitted}
              value={apiKey}
              validation={validateNonEmptyString}
              onChange={newApiKey => setApiKey(newApiKey)}
              onValidate={isValid => setIsApiKeyValid(isValid)} />
          </Col>
        </Row>
        <Row>
          <Col lg="6">
            <ValidatedTextbox
              name="Host or IP"
              placeholder="Enter host or ip"
              alertClassName="mt-3 mb-0"
              errorMessage="Hostname is required."
              isSubmitted={props.isSubmitted}
              value={hostname}
              validation={validateNonEmptyString}
              onChange={newHostname => setHostname(newHostname)}
              onValidate={isValid => setIsHostnameValid(isValid)} />
          </Col>
          <Col lg="6">
            <ValidatedTextbox
              name="Port"
              placeholder="Enter port"
              alertClassName="mt-3 mb-0"
              errorMessage="Please enter a valid port."
              isSubmitted={props.isSubmitted}
              value={port}
              validation={validatePort}
              onChange={newPort => setPort(newPort)}
              onValidate={isValid => setIsPortValid(isValid)} />
          </Col>
        </Row>
        <Row>
          <Col lg="6">
            <Textbox
              name="Base Url"
              placeholder="Enter base url configured in Chaptarr, leave empty if none configured."
              value={baseUrl}
              onChange={newBaseUrl => setBaseUrl(newBaseUrl)} />
          </Col>
          <Col lg="6">
          </Col>
        </Row>
        <Row>
          <Col lg="6">
            <FormGroup className="custom-control custom-control-alternative custom-checkbox mb-3">
              <Input
                className="custom-control-input"
                id="useSSL"
                type="checkbox"
                onChange={onUseSSLChanged}
                checked={useSSL}
              />
              <label
                className="custom-control-label"
                htmlFor="useSSL">
                <span className="text-muted">Use SSL</span>
              </label>
            </FormGroup>
          </Col>
          <Col lg="6">
          </Col>
        </Row>
        <Row>
          <Col>
            <FormGroup className="mt-4">
              {
                testSettingsRequested && !isTestingSettings ?
                  !testSettingsSuccess ? (
                    <Alert className="text-center" color="danger">
                      <strong>{testSettingsError}</strong>
                    </Alert>)
                    : <Alert className="text-center" color="success">
                      <strong>The specified settings are valid.</strong>
                    </Alert>
                  : null
              }
            </FormGroup>
          </Col>
        </Row>
        <Row>
          <Col>
            <FormGroup className="text-right">
              <button onClick={onTestSettings} disabled={!isHostnameValid || !isPortValid || !isApiKeyValid} className="btn btn-icon btn-3 btn-default" type="button">
                <span className="btn-inner--icon">
                  {
                    isTestingSettings ? (
                      <Oval
                        wrapperClass="loader"
                        type="Oval"
                        color="#11cdef"
                        height={19}
                        width={19}
                      />)
                      : (<i className="fas fa-cogs"></i>)
                  }</span>
                <span className="btn-inner--text">Test Settings</span>
              </button>
            </FormGroup>
          </Col>
        </Row>
      </div>
      <div>
        <h6 className="heading-small text-muted mt-4">
          Chaptarr Requests Settings
        </h6>
      </div>
      <div className="pl-lg-4">
        <Row>
          <Col lg="6">
            {/* Plain input: the shared Textbox drops the keystroke that empties the field. */}
            <FormGroup>
              <label
                className="form-control-label"
                htmlFor="input-Root Folder">
                Root Folder
              </label>
              <Input
                value={rootFolder ?? ""}
                onChange={e => setRootFolder(e.target.value)}
                className="form-control-alternative"
                id="input-Root Folder"
                placeholder="Leave empty to use the default configured in Chaptarr."
                type="text" />
            </FormGroup>
          </Col>
        </Row>
        <Row>
          <Col lg="6">
            <Dropdown
              name="Default Search Provider"
              value={searchProvider}
              items={[{ name: "Goodreads", value: "goodreads" }, { name: "Hardcover (needs a Hardcover API token in Chaptarr)", value: "hardcover" }, { name: "Audible", value: "audible" }]}
              onChange={newSearchProvider => setSearchProvider(newSearchProvider)} />
          </Col>
        </Row>
        <Row>
          <Col lg="6">
            <FormGroup className="custom-control custom-control-alternative custom-checkbox mb-3">
              <Input
                className="custom-control-input"
                id="SearchNewRequests"
                type="checkbox"
                onChange={e => { setSearchNewRequests(!searchNewRequests); }}
                checked={searchNewRequests}
              />
              <label
                className="custom-control-label"
                htmlFor="SearchNewRequests">
                <span className="text-muted">Automatically search for media when request is made</span>
              </label>
            </FormGroup>
          </Col>
        </Row>
      </div>
    </>
  );
}

export default Chaptarr;
