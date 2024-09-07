import { Route, RouterProvider, createBrowserRouter, createRoutesFromElements } from "react-router-dom";
import Home from "./pages/Home";
import Chat from "./pages/Chat";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ServiceContextProvider } from "./services/useApi";
import { Toaster } from "./components/ui/sonner";

export const router = createBrowserRouter(
    createRoutesFromElements(
        <>
            <Route path="/" element={<Home />} />
            <Route path="/context/:id" element={<Chat />} />
            <Route path="*" element={<p>Error</p>} />
        </>
    )
);

const client = new QueryClient({
    defaultOptions: {
      queries: {
        refetchOnWindowFocus: false,
        refetchOnReconnect: false,
        retry: false
      },
    },
  });

const App = () => {
    return <ServiceContextProvider>
        <QueryClientProvider client={client}>
            <Toaster />
            <RouterProvider router={router} />
        </QueryClientProvider>
    </ServiceContextProvider>
}

export default App;