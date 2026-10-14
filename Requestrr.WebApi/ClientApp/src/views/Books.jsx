
import { useEffect, useState } from "react";
import { useDispatch, useSelector } from 'react-redux';
import { Alert } from "reactstrap";
import { getSettings } from "../store/actions/BooksClientsActions";
import { saveDisabledClient } from "../store/actions/BooksClientsActions"
import { saveChaptarrClient } from "../store/actions/ChaptarrClientActions";
import Dropdown from "../components/Inputs/Dropdown"
import Chaptarr from "../components/DownloadClients/Chaptarr/Chaptarr";

// reactstrap components
import {
  Button,
  Card,
  CardHeader,
  CardBody,
  FormGroup,
  Form,
  Container,
  Row,
  Col
} from "reactstrap";
// core components
import UserHeader from "../components/Headers/UserHeader.jsx";


function Books(props) {
  const [isSubmitted, setIsSubmitted] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [saveAttempted, setSaveAttempted] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [client, setClient] = useState("");
  const [chaptarr, setChaptarr] = useState({});
  const [isChaptarrValid, setIsChaptarrValid] = useState(false);


  const reduxState = useSelector((state) => {
    return {
      settings: state.books
    };
  });
  const dispatch = useDispatch();


  useEffect(() => {
    dispatch(getSettings())
      .then(data => {
        setIsLoading(false);
        setClient(data.payload.client);
        setChaptarr(data.payload.chaptarr);
      });
  }, []);


  useEffect(() => {
    if (!isSubmitted)
      return;

    if (!isSaving) {
      if ((client === "Disabled"
        || (client === "Chaptarr"
          && isChaptarrValid)
      )) {
        setIsSaving(true);

        let saveAction = null;

        if (client === "Disabled") {
          saveAction = dispatch(saveDisabledClient());
        } else if (client === "Chaptarr") {
          saveAction = dispatch(saveChaptarrClient({
            chaptarr: chaptarr,
          }));
        }

        saveAction.then(data => {
          setIsSaving(false);
          setIsSubmitted(false);

          if (data.ok) {
            setSaveAttempted(true);
            setSaveError("");
            setSaveSuccess(true);
          } else {
            var error = "An unknown error occurred while saving.";

            if (typeof (data.error) === "string")
              error = data.error;

            setSaveAttempted(true);
            setSaveError(error);
            setSaveSuccess(false);
          }
        });
      } else {
        setSaveAttempted(true);
        setSaveError("Some fields are invalid, please fix them before saving.");
        setSaveSuccess(false);
        setIsSubmitted(false);
      }
    }
  }, [isSubmitted]);


  const onClientChange = () => {
    setChaptarr(reduxState.settings.chaptarr);

    setSaveAttempted(false);
    setIsSubmitted(false);
  }

  const onSaving = (e) => {
    e.preventDefault();
    setIsSubmitted(true);
  }

  return (
    <>
      <UserHeader title="Books" description="This page is for configuring the connection between your bot and Chaptarr" />
      <Container className="mt--7" fluid>
        <Row>
          <Col className="order-xl-1" xl="12">
            <Card className="bg-secondary shadow">
              <CardHeader className="bg-white border-0">
                <Row className="align-items-center">
                  <Col xs="8">
                    <h3 className="mb-0">Configuration</h3>
                  </Col>
                  <Col className="text-right" xs="4">
                    <Button
                      color="primary"
                      href="#pablo"
                      onClick={e => e.preventDefault()}
                      size="sm"
                    >
                      Settings
                    </Button>
                  </Col>
                </Row>
              </CardHeader>
              <CardBody className={isLoading ? "fade" : "fade show"}>
                <Form className="complex">
                  <h6 className="heading-small text-muted mb-4">
                    General Settings
                  </h6>
                  <div className="pl-lg-4">
                    <Row>
                      <Col lg="6">
                        <Dropdown
                          name="Download Client"
                          value={client}
                          items={[{ name: "Disabled", value: "Disabled" }, { name: "Chaptarr", value: "Chaptarr" }]}
                          onChange={newClient => { setClient(newClient); onClientChange(); }}
                        />
                      </Col>
                    </Row>
                  </div>
                  {
                    client !== "Disabled"
                      ? <>
                        <hr className="my-4" />
                        {
                          client == "Chaptarr" ?
                            <>
                              <Chaptarr
                                onChange={values => setChaptarr(values)}
                                onValidate={value => setIsChaptarrValid(value)}
                                isSubmitted={isSubmitted}
                                isSaving={isSaving}
                              />
                            </>
                            : null
                        }
                      </>
                      : null
                  }
                  <div className="pl-lg-4">
                    <Row>
                      <Col>
                        <FormGroup className="mt-4">
                          {
                            saveAttempted && !isSaving ?
                              !saveSuccess ? (
                                <Alert className="text-center" color="danger">
                                  <strong>{saveError}</strong>
                                </Alert>)
                                : <Alert className="text-center" color="success">
                                  <strong>Settings updated successfully.</strong>
                                </Alert>
                              : null
                          }
                        </FormGroup>
                        <FormGroup className="text-right">
                          <button className="btn btn-icon btn-3 btn-primary" onClick={onSaving} disabled={isSaving} type="button">
                            <span className="btn-inner--icon"><i className="fas fa-save"></i></span>
                            <span className="btn-inner--text">Save Changes</span>
                          </button>
                        </FormGroup>
                      </Col>
                    </Row>
                  </div>
                </Form>
              </CardBody>
            </Card>
          </Col>
        </Row>
      </Container>
    </>
  );
}
export default Books;
