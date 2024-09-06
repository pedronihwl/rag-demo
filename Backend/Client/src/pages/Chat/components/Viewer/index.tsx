import { ScrollArea } from "@/components/ui/scroll-area";
import { useEffect, useState } from "react";
import { pdfjs, Document, Page } from "react-pdf";

import "react-pdf/dist/Page/AnnotationLayer.css";
import "react-pdf/dist/Page/TextLayer.css";

type IProps = {
    blob: Blob
}

const Viewer = ({ blob }: IProps) => {
    const [pages, setPages] = useState(0)
    const [focused, setFocused] = useState(1)

    useEffect(() => {
        pdfjs.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.4.168/pdf.worker.min.mjs';
    }, []);

    const onLoad = (event: any) => {
        setPages(event.numPages);
    }

    return <ScrollArea className="w-full h-[calc(100%-30px)] my-4 overflow-y-auto flex justify-center items-start">
        <Document className="mx-4 w-fit" file={blob} onLoadSuccess={onLoad}>
            {
                Array.from({ length: pages }, (_, i) => i + 1).map((item) => (
                    <div
                        onClick={() => setFocused(item)}
                        className={`border-[4px] cursor-pointer relative rounded my-1 ${focused === item ? "border-green-700" : ""}`}
                    >
                        <Page
                        pageIndex={item - 1}
                        pageNumber={item}
                        ></Page>
                    </div>
                ))
            }
        </Document>
    </ScrollArea>

}


/**
 * 
 * <div
                    onClick={() => setFocused(index + 1)}
                    className={`border-[4px] cursor-pointer relative rounded my-2 ${focused === index ? "border-green-700" : ""}`}
                  >
                    <Page
                      pageIndex={index + 1}
                      pageNumber={index}
                    ></Page>
                  </div>
 */

export default Viewer;