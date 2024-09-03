import { FileType } from "@/pages/Chat/components/FileContainer";

const fileMocks: FileType[] = [
    {
        name: "Document1.pdf",
        status: "Processing",
        totalPages: 120,
        processedPages: 45,
        chunks: 10
    },
    {
        name: "Report2024.pdf",
        status: "Completed",
        totalPages: 80,
        processedPages: 80,
        chunks: 15
    },
    {
        name: "Draft_Notes.pdf",
        status: "Pending",
        totalPages: 50,
        processedPages: 0,
        chunks: 5
    }
];

export {
    fileMocks
}