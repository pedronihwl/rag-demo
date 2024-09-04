import React, { useMemo, PropsWithChildren, useContext } from "react";
import { HttpClient } from "./api";

interface IServiceProps {
  api: HttpClient;
}

const ServiceContext = React.createContext<IServiceProps>({ } as IServiceProps);

export const ServiceContextProvider = ({ children }: PropsWithChildren) => {
    const apiClient = useMemo(() => new HttpClient(), []);
    
    return (
        <ServiceContext.Provider value={{ api: apiClient }}>
        {children}
      </ServiceContext.Provider>
    );
  };
  
  export const useApi = () => useContext(ServiceContext);