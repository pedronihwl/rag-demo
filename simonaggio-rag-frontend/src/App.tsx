import { Route, RouterProvider, createBrowserRouter, createRoutesFromElements } from "react-router-dom";
import Home from "./pages/Home";
import Chat from "./pages/Chat";

export const router = createBrowserRouter(
    createRoutesFromElements(
        <>
            <Route path="/" element={<Home />} />
            <Route path="/context/:id" element={<Chat/>} />
            <Route path="*" element={<p>Error</p>} />
        </>
    )
);

const App = () => {
    return <RouterProvider router={router} />
}

export default App;